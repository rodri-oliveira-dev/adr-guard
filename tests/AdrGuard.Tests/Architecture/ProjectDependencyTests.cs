using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace AdrGuard.Tests.Architecture;

public sealed class ProjectDependencyTests
{
    private static readonly string CoreProject = Path.Combine("src", "AdrGuard.Core", "AdrGuard.Core.csproj");
    private static readonly string CliProject = Path.Combine("src", "AdrGuard", "AdrGuard.csproj");
    private static readonly string McpProject = Path.Combine("src", "AdrGuard.Mcp", "AdrGuard.Mcp.csproj");

    [Fact]
    public void CoreIsAnInternalClassLibraryWithoutExecutableReferences()
    {
        var document = LoadProject(CoreProject);

        Assert.Null(Property(document, "OutputType"));
        Assert.Equal("false", Property(document, "IsPackable"));
        Assert.Empty(ProjectReferences(CoreProject));
    }

    [Fact]
    public void CliReferencesCoreDirectly()
    {
        var references = ProjectReferences(CliProject);

        Assert.Contains(
            Normalize(CoreProject),
            references,
            StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Normalize(McpProject), references, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void McpReferencesCoreWithoutCliProjectReference()
    {
        var references = ProjectReferences(McpProject);

        Assert.Contains(Normalize(CoreProject), references, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Normalize(CliProject), references, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CliDoesNotGrantInternalAccessToMcp()
    {
        var cliProject = LoadProject(CliProject);
        Assert.DoesNotContain(
            cliProject.Descendants().Where(element => element.Name.LocalName == "InternalsVisibleTo"),
            element => string.Equals(
                element.Attribute("Include")?.Value,
                "adr-guard-mcp",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void McpSourceDoesNotUseCliNamespace()
    {
        var mcpSourceRoot = Path.Combine(RepositoryRoot(), "src", "AdrGuard.Mcp");
        var cliNamespaceUsages = Directory.EnumerateFiles(mcpSourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(mcpSourceRoot, path);
                return !relative.StartsWith($"bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                       && !relative.StartsWith($"obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
            })
            .Where(path => File.ReadAllText(path).Contains("AdrGuard.Cli", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot(), path))
            .ToArray();
        Assert.Empty(cliNamespaceUsages);
    }

    [Fact]
    public void CurrentProductionProjectGraphIsAcyclic()
    {
        var projects = new[] { CoreProject, CliProject, McpProject };
        var graph = projects.ToDictionary(
            project => Normalize(project),
            project => ProjectReferences(project).ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var project in graph.Keys)
        {
            Assert.False(HasCycle(project, graph, [], []), $"Project dependency cycle starts at '{project}'.");
        }
    }

    private static bool HasCycle(
        string project,
        IReadOnlyDictionary<string, HashSet<string>> graph,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(project)) return false;
        if (!visiting.Add(project)) return true;

        if (graph.TryGetValue(project, out var references))
        {
            foreach (var reference in references.Where(graph.ContainsKey))
            {
                if (HasCycle(reference, graph, visiting, visited)) return true;
            }
        }

        visiting.Remove(project);
        visited.Add(project);
        return false;
    }

    private static string[] ProjectReferences(string project)
    {
        var projectPath = Path.Combine(RepositoryRoot(), project);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;

        return LoadProject(project)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Normalize(Path.GetRelativePath(RepositoryRoot(), Path.GetFullPath(include!, projectDirectory))))
            .ToArray();
    }

    private static XDocument LoadProject(string project)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(Path.Combine(RepositoryRoot(), project), settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string? Property(XDocument document, string name) =>
        document.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value;

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AdrGuard.slnx"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the ADR Guard repository root.");
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
