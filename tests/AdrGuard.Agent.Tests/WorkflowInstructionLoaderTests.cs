using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class WorkflowInstructionLoaderTests
{
    [Theory]
    [InlineData("---\nname: adr-guard-validate\n")]
    [InlineData("---\nname: adr-guard-validate\nname: adr-guard-create\n---\nInstructions")]
    public async Task MalformedFrontMatterIsRejected(string content)
    {
        using var repository = TemporaryDirectory.Create();
        var skillDirectory = Path.Combine(repository.Path, "skills", "adr-guard-validate");
        Directory.CreateDirectory(skillDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(skillDirectory, "SKILL.md"),
            content,
            TestContext.Current.CancellationToken);

        var result = await WorkflowInstructionLoader.Instance.LoadAsync(
            CreateConfiguration(repository.Path),
            ArchitectureWorkflowCatalog.ConsultAndValidateAdrs,
            TestContext.Current.CancellationToken);

        Assert.Null(result.Content);
        Assert.Contains("does not declare the expected skill", result.MissingInformation, StringComparison.Ordinal);
    }

    private static AgentConfiguration CreateConfiguration(string repositoryRoot) =>
        new(
            repositoryRoot,
            "docs/adr",
            "fake-mcp",
            [],
            TimeSpan.FromSeconds(5),
            "openai",
            "fake-model",
            "Validate the ADRs",
            true,
            AgentLimits.Default,
            null,
            "HEAD",
            null,
            [],
            null,
            "advisory");

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        internal string Path { get; }

        internal static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"adr-guard-agent-skill-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
