'use strict';

// Adapted from the repository's Dapper-FluentMap high-impact Sonar synchronizer.
// The workflow runs only on the trusted default branch, never on pull requests.
module.exports = async function syncSonar({github, context, core}) {
  const { owner, repo } = context.repo;
  const sonarHost = process.env.SONAR_HOST_URL.replace(/\/+$/, "");
  const projectKey = process.env.SONAR_PROJECT_KEY;
  const labelName = process.env.SONAR_ISSUE_LABEL;
  const sonarToken = process.env.SONAR_TOKEN;
  if (!projectKey || !sonarToken) throw new Error('SONAR_PROJECT_KEY and SONAR_TOKEN are required.');
  const workflowAuthor = "github-actions[bot]";
  const trackedSeverities = new Set(["BLOCKER", "HIGH"]);
  const severityRank = new Map([
    ["BLOCKER", 2],
    ["HIGH", 1],
  ]);
  const syncStart = "<!-- sonar-sync:start -->";
  const syncEnd = "<!-- sonar-sync:end -->";
  
  core.setSecret(sonarToken);
  
  async function sonarGet(path, params) {
    const url = new URL(`${sonarHost}${path}`);
  
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== "") {
        url.searchParams.set(key, String(value));
      }
    }
  
    const response = await fetch(url, {
      headers: {
        Accept: "application/json",
        Authorization: `Bearer ${sonarToken}`,
      },
    });
  
    if (!response.ok) {
      throw new Error(
        `Sonar API ${path} failed with HTTP ${response.status}.`,
      );
    }
  
    return response.json();
  }
  
  function markdownInline(value) {
    return String(value ?? "")
      .replace(/\r?\n/g, " ")
      .replace(/`/g, "\\`");
  }
  
  function quoteMarkdown(value) {
    return String(value ?? "Sem mensagem fornecida pelo Sonar.")
      .split(/\r?\n/)
      .map((line) => `> ${line}`)
      .join("\n");
  }
  
  function componentPath(component) {
    const prefix = `${projectKey}:`;
    return component?.startsWith(prefix)
      ? component.slice(prefix.length)
      : component ?? "Component not supplied";
  }
  
  function encodeRepositoryPath(path) {
    return path
      .split("/")
      .map((segment) => encodeURIComponent(segment))
      .join("/");
  }
  
  function managedBody(existingBody, nextManagedBody) {
    if (!existingBody) {
      return [
        nextManagedBody,
        "",
        "<!-- Manual notes may be added below. This section is preserved by the workflow. -->",
        "",
      ].join("\n");
    }
  
    const startIndex = existingBody.indexOf(syncStart);
    const endIndex = existingBody.indexOf(syncEnd);
  
    if (startIndex === -1 || endIndex === -1 || endIndex < startIndex) {
      return existingBody;
    }
  
    const suffixStart = endIndex + syncEnd.length;
    return `${existingBody.slice(0, startIndex)}${nextManagedBody}${existingBody.slice(suffixStart)}`;
  }
  
  function extractManagedSonarIssueKey(githubIssue) {
    // issues.listForRepo also returns pull requests.
    if (githubIssue.pull_request) {
      return null;
    }
  
    // GITHUB_TOKEN creates issues as github-actions[bot]. Requiring that
    // provenance prevents user-authored issues from spoofing public Sonar keys.
    if (githubIssue.user?.login !== workflowAuthor) {
      return null;
    }
  
    const body = githubIssue.body ?? "";
    const startIndex = body.indexOf(syncStart);
    if (startIndex === -1) {
      return null;
    }
  
    const endIndex = body.indexOf(syncEnd, startIndex + syncStart.length);
    if (endIndex === -1 || endIndex < startIndex) {
      return null;
    }
  
    const managedSection = body.slice(startIndex, endIndex + syncEnd.length);
    const match = managedSection.match(/<!--\s*sonar-issue-key:([^\s>]+)\s*-->/);
    return match?.[1] ?? null;
  }
  
  async function ensureLabel() {
    try {
      await github.rest.issues.getLabel({ owner, repo, name: labelName });
      return;
    } catch (error) {
      if (error.status !== 404) {
        throw error;
      }
    }
  
    await github.rest.issues.createLabel({
      owner,
      repo,
      name: labelName,
      color: "b60205",
      description: "High-impact quality findings synchronized from SonarQube Cloud",
    });
  }
  
  const repository = await github.rest.repos.get({ owner, repo });
  const defaultBranch = repository.data.default_branch;
  
  const sonarIssues = [];
  const rules = new Map();
  const pageSize = 500;
  
  for (let page = 1; page <= 100; page += 1) {
    const response = await sonarGet("/api/issues/search", {
      componentKeys: projectKey,
      issueStatuses: "OPEN",
      impactSeverities: "BLOCKER,HIGH",
      additionalFields: "_all",
      ps: pageSize,
      p: page,
    });
  
    const pageIssues = response.issues ?? [];
    sonarIssues.push(...pageIssues);
  
    for (const rule of response.rules ?? []) {
      if (rule.key) {
        rules.set(rule.key, rule);
      }
    }
  
    const total = response.paging?.total ?? response.total ?? sonarIssues.length;
    if (sonarIssues.length >= total || pageIssues.length < pageSize) {
      break;
    }
  
    if (page === 100) {
      throw new Error("Sonar issue pagination exceeded the 100-page safety limit.");
    }
  }
  
  const findings = sonarIssues.map((issue) => {
    const qualifyingImpacts = (issue.impacts ?? []).filter((impact) =>
      trackedSeverities.has(impact.severity),
    );
  
    if (qualifyingImpacts.length === 0) {
      throw new Error(
        `Sonar returned issue ${issue.key} for impactSeverities=BLOCKER,HIGH without a matching impacts entry.`,
      );
    }
  
    return { issue, qualifyingImpacts };
  });
  
  await ensureLabel();
  
  // Scan all repository issues so removing the managed label cannot create
  // duplicates. Provenance and delimiters are validated before accepting a match.
  const repositoryIssues = await github.paginate(
    github.rest.issues.listForRepo,
    {
      owner,
      repo,
      state: "all",
      per_page: 100,
    },
  );
  
  const existingBySonarKey = new Map();
  
  for (const githubIssue of repositoryIssues) {
    const sonarKey = extractManagedSonarIssueKey(githubIssue);
    if (!sonarKey) {
      continue;
    }
  
    if (existingBySonarKey.has(sonarKey)) {
      core.warning(
        `Multiple workflow-managed GitHub issues reference Sonar issue ${sonarKey}. Keeping #${existingBySonarKey.get(sonarKey).number}.`,
      );
      continue;
    }
  
    existingBySonarKey.set(sonarKey, githubIssue);
  }
  
  let created = 0;
  let updated = 0;
  let reopened = 0;
  let unchanged = 0;
  
  for (const { issue, qualifyingImpacts } of findings) {
    const maxSeverity = qualifyingImpacts
      .map((impact) => impact.severity)
      .sort(
        (left, right) =>
          (severityRank.get(right) ?? 0) - (severityRank.get(left) ?? 0),
      )[0];
  
    const affectedQualities = [
      ...new Set(
        qualifyingImpacts
          .map((impact) => impact.softwareQuality)
          .filter(Boolean),
      ),
    ];
  
    const rule = rules.get(issue.rule);
    const ruleName = rule?.name ?? issue.rule;
    const path = componentPath(issue.component);
    const line = issue.line ?? issue.textRange?.startLine ?? null;
    const endLine = issue.textRange?.endLine ?? line;
    const locationLabel = line
      ? `${path}:${line}${endLine && endLine !== line ? `-${endLine}` : ""}`
      : path;
  
    const fileUrl =
      `https://github.com/${owner}/${repo}/blob/` +
      `${encodeURIComponent(defaultBranch)}/${encodeRepositoryPath(path)}` +
      `${line ? `#L${line}` : ""}`;
  
    const sonarIssueUrl =
      `${sonarHost}/project/issues?open=${encodeURIComponent(issue.key)}` +
      `&id=${encodeURIComponent(projectKey)}`;
  
    const sonarRuleUrl =
      `${sonarHost}/coding_rules?open=${encodeURIComponent(issue.rule)}` +
      `&rule_key=${encodeURIComponent(issue.rule)}`;
  
    const projectIssuesUrl =
      `${sonarHost}/project/issues?impactSeverities=BLOCKER%2CHIGH` +
      `&issueStatuses=OPEN&id=${encodeURIComponent(projectKey)}`;
  
    const relatedLocations = (issue.flows ?? [])
      .flatMap((flow) => flow.locations ?? [])
      .slice(0, 5)
      .map((location) => {
        const relatedPath = componentPath(location.component);
        const relatedLine = location.textRange?.startLine ?? null;
        const relatedFileUrl =
          `https://github.com/${owner}/${repo}/blob/` +
          `${encodeURIComponent(defaultBranch)}/${encodeRepositoryPath(relatedPath)}` +
          `${relatedLine ? `#L${relatedLine}` : ""}`;
        const relatedLabel = relatedLine
          ? `${relatedPath}:${relatedLine}`
          : relatedPath;
        const relatedMessage = location.msg
          ? ` — ${markdownInline(location.msg)}`
          : "";
  
        return `- [\`${markdownInline(relatedLabel)}\`](${relatedFileUrl})${relatedMessage}`;
      });
  
    const impactText = qualifyingImpacts
      .map(
        (impact) =>
          `\`${markdownInline(impact.softwareQuality ?? "UNKNOWN")}\`: **${markdownInline(impact.severity)}**`,
      )
      .join(", ");
  
    const tags =
      (issue.tags ?? []).length > 0
        ? issue.tags
            .map((tag) => `\`${markdownInline(tag)}\``)
            .join(", ")
        : "Not supplied";
  
    const managedLines = [
      syncStart,
      `<!-- sonar-issue-key:${issue.key} -->`,
      "> Sincronizada automaticamente do SonarQube Cloud. Enquanto o finding permanecer **OPEN** com impacto **HIGH** ou **BLOCKER**, esta issue representa o mesmo problema.",
      "",
      "## Summary",
      "",
      `- **Impacto:** ${impactText}`,
      `- **Affected software quality:** ${
        affectedQualities.length > 0
          ? affectedQualities
              .map((quality) => `\`${markdownInline(quality)}\``)
              .join(", ")
          : "Not supplied"
      }`,
      `- **Rule:** [${markdownInline(ruleName)} (\`${markdownInline(issue.rule)}\`)](${sonarRuleUrl})`,
      `- **Clean Code attribute:** ${
        issue.cleanCodeAttribute
          ? `\`${markdownInline(issue.cleanCodeAttribute)}\``
          : "Not supplied"
      }`,
      `- **Category:** ${
        issue.cleanCodeAttributeCategory
          ? `\`${markdownInline(issue.cleanCodeAttributeCategory)}\``
          : "Not supplied"
      }`,
      `- **Location:** [\`${markdownInline(locationLabel)}\`](${fileUrl})`,
      `- **Estimated effort:** ${
        issue.effort ? `\`${markdownInline(issue.effort)}\`` : "Not supplied"
      }`,
      `- **Quick fix available:** ${
        issue.quickFixAvailable === true
          ? "Yes"
          : issue.quickFixAvailable === false
            ? "No"
            : "Not supplied"
      }`,
      `- **Tags:** ${tags}`,
      `- **Created in Sonar:** ${
        issue.creationDate
          ? `\`${markdownInline(issue.creationDate)}\``
          : "Not supplied"
      }`,
      `- **Updated in Sonar:** ${
        issue.updateDate
          ? `\`${markdownInline(issue.updateDate)}\``
          : "Not supplied"
      }`,
      "",
      "## Sonar finding",
      "",
      quoteMarkdown(issue.message),
      "",
      "## Suggested remediation",
      "",
      "1. Open the original Sonar finding and inspect primary and secondary locations.",
      `2. Review rule **${markdownInline(ruleName)}** and identify the root cause rather than suppressing the finding.`,
      "3. Apply the smallest fix that addresses the cause without unrelated changes.",
      "4. Add or update regression tests when behavior, contracts, or edge cases are affected.",
      "5. Run the relevant build and tests and check for regressions.",
      "6. Rerun Sonar analysis and confirm the finding no longer remains OPEN.",
    ];
  
    if (relatedLocations.length > 0) {
      managedLines.push(
        "",
        "## Related locations",
        "",
        ...relatedLocations,
      );
    }
  
    managedLines.push(
      "",
      "## Links",
      "",
      `- [Abrir este finding no Sonar](${sonarIssueUrl})`,
      `- [Abrir a regra no Sonar](${sonarRuleUrl})`,
      `- [Ver todos os findings HIGH/BLOCKER do projeto](${projectIssuesUrl})`,
      "",
      syncEnd,
    );
  
    const managed = managedLines.join("\n");
  
    const qualityPrefix =
      affectedQualities.length > 0
        ? `[${affectedQualities.join("/")}]`
        : "";
  
    const rawTitle = `[Sonar][${maxSeverity}]${qualityPrefix} ${ruleName}`;
    const title =
      rawTitle.length <= 240
        ? rawTitle
        : `${rawTitle.slice(0, 237)}...`;
  
    const existing = existingBySonarKey.get(issue.key);
  
    if (!existing) {
      const createdIssue = await github.rest.issues.create({
        owner,
        repo,
        title,
        body: managedBody("", managed),
        labels: [labelName],
      });
  
      existingBySonarKey.set(issue.key, createdIssue.data);
      core.info(
        `Created GitHub issue #${createdIssue.data.number} for Sonar issue ${issue.key}.`,
      );
      created += 1;
      continue;
    }
  
    const nextBody = managedBody(existing.body ?? "", managed);
    const needsUpdate =
      existing.title !== title || existing.body !== nextBody;
    const needsReopen = existing.state === "closed";
    const hasLabel = (existing.labels ?? []).some((label) =>
      typeof label === "string"
        ? label === labelName
        : label?.name === labelName,
    );
  
    if (needsUpdate || needsReopen) {
      await github.rest.issues.update({
        owner,
        repo,
        issue_number: existing.number,
        title,
        body: nextBody,
        state: needsReopen ? "open" : undefined,
      });
  
      updated += 1;
      if (needsReopen) {
        reopened += 1;
      }
    }
  
    if (!hasLabel) {
      await github.rest.issues.addLabels({
        owner,
        repo,
        issue_number: existing.number,
        labels: [labelName],
      });
    }
  
    if (!needsUpdate && !needsReopen && hasLabel) {
      unchanged += 1;
    }
  }
  
  core.info(
    `Sonar sync complete: ${findings.length} tracked finding(s), ${created} created, ${updated} updated, ${reopened} reopened, ${unchanged} unchanged.`,
  );
  
  await core.summary
    .addHeading("Sonar Quality Issues sync")
    .addTable([
      [
        { data: "Metric", header: true },
        { data: "Count", header: true },
      ],
      ["Tracked HIGH/BLOCKER findings", String(findings.length)],
      ["Created", String(created)],
      ["Updated", String(updated)],
      ["Reopened", String(reopened)],
      ["Unchanged", String(unchanged)],
    ])
    .write();
  
};
