using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrPlaceholderPolicyTests
{
    [Fact]
    public void PlaceholderPolicyIsOffByDefaultAndSupportsWarningOrError()
    {
        var document = AdrMarkdownParser.Parse("0001-draft.md", Canonical("[EDITAR] {{owner}}"));
        Assert.True(AdrValidator.Validate([document]).IsValid);

        var warning = AdrValidator.Validate([document], null, new AdrValidationOptions(AdrFormat.Canonical, PlaceholderPolicy: PlaceholderPolicy.Warn));
        var error = AdrValidator.Validate([document], null, new AdrValidationOptions(AdrFormat.Canonical, PlaceholderPolicy: PlaceholderPolicy.Error));

        Assert.True(warning.IsValid);
        Assert.All(warning.Issues, issue => Assert.Equal(ValidationSeverity.Warning, issue.Severity));
        Assert.False(error.IsValid);
        Assert.Contains(error.Issues, issue => issue.Code == ValidationCodes.UnresolvedPlaceholder);
    }

    [Fact]
    public void FencedExamplesAreIgnored()
    {
        var document = AdrMarkdownParser.Parse("0001-draft.md", Canonical("```text\n[EDIT] {{example}}\n```"));
        var result = AdrValidator.Validate([document], null, new AdrValidationOptions(AdrFormat.Canonical, PlaceholderPolicy: PlaceholderPolicy.Error));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Todos os componentes possuem responsáveis.")]
    [InlineData("O método todos() é apenas um exemplo.")]
    [InlineData("This methodology is documented.")]
    [InlineData("Already completedTODOtask should not match.")]
    public void PlaceholderWordsMustNotMatchSubstrings(string content)
    {
        var document = AdrMarkdownParser.Parse("0001-draft.md", Canonical(content));
        var result = AdrValidator.Validate(
            [document], null, new AdrValidationOptions(AdrFormat.Canonical, PlaceholderPolicy: PlaceholderPolicy.Error));

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == ValidationCodes.UnresolvedPlaceholder);
    }

    [Theory]
    [InlineData("TODO: document migration")]
    [InlineData("(tbd) before rollout")]
    [InlineData("Complete [EDIT] before merging")]
    [InlineData("[EDITAR] antes da publicação")]
    public void StandalonePlaceholderMarkersAreStillDetected(string content)
    {
        var document = AdrMarkdownParser.Parse("0001-draft.md", Canonical(content));
        var result = AdrValidator.Validate(
            [document], null, new AdrValidationOptions(AdrFormat.Canonical, PlaceholderPolicy: PlaceholderPolicy.Error));

        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.UnresolvedPlaceholder);
    }

    private static string Canonical(string content) => $$"""
        # Draft
        ## Status
        Proposed
        ## Context
        {{content}}
        ## Decision
        Decision.
        ## Consequences
        Consequences.
        """;
}
