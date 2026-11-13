using System.Text;

namespace AdrGuard.Agent;

internal sealed class WorkflowInstructionLoader : IWorkflowInstructionLoader
{
    internal const int MaximumInstructionCharacters = 32_768;

    internal static WorkflowInstructionLoader Instance { get; } = new();

    public async Task<WorkflowInstructionLoadResult> LoadAsync(
        AgentConfiguration configuration,
        ArchitectureWorkflow workflow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(workflow);

        if (!ArchitectureWorkflowCatalog.AuthorizedSkillNames.Contains(
                workflow.SkillName,
                StringComparer.Ordinal)
            || !string.Equals(
                workflow.SkillPath,
                $"skills/{workflow.SkillName}/SKILL.md",
                StringComparison.Ordinal))
        {
            throw new AgentExecutionException(
                "The selected workflow references an unauthorized instruction file.");
        }

        var instructionPath = Path.GetFullPath(workflow.SkillPath, configuration.RepositoryRoot);
        var expectedPath = Path.GetFullPath(
            Path.Combine("skills", workflow.SkillName, "SKILL.md"),
            configuration.RepositoryRoot);
        if (!string.Equals(instructionPath, expectedPath, PathComparison))
        {
            throw new AgentExecutionException(
                "The selected workflow instruction path escaped its authorized location.");
        }

        if (!File.Exists(instructionPath))
        {
            return new WorkflowInstructionLoadResult(
                null,
                $"The selected workflow requires '{workflow.SkillPath}', but that authorized skill file is unavailable.");
        }

        try
        {
            RejectReparsePoints(configuration.RepositoryRoot, workflow.SkillName);
            var fileInfo = new FileInfo(instructionPath);
            if (fileInfo.Length > MaximumInstructionCharacters * 4L)
            {
                return TooLarge(workflow);
            }

            await using var stream = new FileStream(
                instructionPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4_096,
                useAsync: true);
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4_096,
                leaveOpen: false);
            var buffer = new char[MaximumInstructionCharacters + 1];
            var charactersRead = await reader.ReadBlockAsync(
                buffer.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            if (charactersRead > MaximumInstructionCharacters)
            {
                return TooLarge(workflow);
            }

            var content = new string(buffer, 0, charactersRead);
            if (!HasExpectedSkillName(content, workflow.SkillName))
            {
                return new WorkflowInstructionLoadResult(
                    null,
                    $"The selected workflow instruction file does not declare the expected skill '{workflow.SkillName}'.");
            }

            return new WorkflowInstructionLoadResult(content, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AgentExecutionException(
                "The selected workflow instruction file could not be read safely.",
                exception);
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool HasExpectedSkillName(string content, string skillName)
    {
        using var reader = new StringReader(content);
        if (!string.Equals(reader.ReadLine(), "---", StringComparison.Ordinal))
        {
            return false;
        }

        string? line;
        while ((line = reader.ReadLine()) is not null && line != "---")
        {
            if (string.Equals(line, $"name: {skillName}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void RejectReparsePoints(string repositoryRoot, string skillName)
    {
        var paths = new[]
        {
            Path.Combine(repositoryRoot, "skills"),
            Path.Combine(repositoryRoot, "skills", skillName),
            Path.Combine(repositoryRoot, "skills", skillName, "SKILL.md"),
        };
        if (paths.Any(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
        {
            throw new AgentExecutionException(
                "The selected workflow instruction path contains a symbolic link or reparse point.");
        }
    }

    private static WorkflowInstructionLoadResult TooLarge(ArchitectureWorkflow workflow) =>
        new(
            null,
            $"The selected workflow instruction file exceeds the {MaximumInstructionCharacters}-character instruction limit: '{workflow.SkillPath}'.");
}
