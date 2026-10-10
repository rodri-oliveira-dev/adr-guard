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
