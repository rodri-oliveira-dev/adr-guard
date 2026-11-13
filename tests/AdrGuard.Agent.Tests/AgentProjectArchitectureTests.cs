using System.Xml.Linq;
using Xunit;

namespace AdrGuard.Agent.Tests;

public sealed class AgentProjectArchitectureTests
{
    [Fact]
    public void AgentProjectHasNoAssemblyReferencesToExistingProducts()
    {
        var projectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/AdrGuard.Agent/AdrGuard.Agent.csproj"));
        var project = XDocument.Load(projectPath);
        var references = project
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(reference => reference is not null)
            .ToArray();

        Assert.Empty(references);
    }
}
