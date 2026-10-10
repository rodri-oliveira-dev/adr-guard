using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrMetadataTests
{
    [Fact]
    public void OptionalMetadataIsTypedWithoutChangingCanonicalStatusAuthority()
    {
        var document = AdrMarkdownParser.Parse("0001-decision.md", """
            ---
            status: Rejected
            date: 2026-10-09
            last-reviewed: 2026-10-10
            owner: Architecture Team
            decision-makers: Alice, Bob
            requirements: ASR-12; ASR-14
            follow-ups: https://example.invalid/issues/1
            category: platform
            ---
            # Decision
            ## Status
            Accepted
            ## Context
            Context.
            ## Decision
            Decision.
            ## Consequences
            Consequences.
            """);

        Assert.Equal("Accepted", document.Status);
        Assert.Equal("Architecture Team", document.DecisionMetadata!.Owner);
        Assert.Equal(["Alice", "Bob"], document.DecisionMetadata.DecisionMakers);
        Assert.True(AdrValidator.Validate([document], null, new AdrValidationOptions(AdrFormat.Canonical, ValidateMetadata: true)).IsValid);
    }

    [Fact]
    public void QuotedAndOrdinaryMetadataScalarsAllowYamlMarkerCharacters()
    {
        var document = AdrMarkdownParser.Parse("0001-decision.md", """
            ---
            owner: "R&D"
            category: 'Platform!'
            follow-ups: https://example.invalid/issues?draft=1&owner=2
            decision-makers: Alice * Bob
            ---
            # Decision
            ## Status
            Accepted
            ## Context
            Context.
            ## Decision
            Decision.
            ## Consequences
            Consequences.
            """);

        var result = AdrValidator.Validate(
            [document], null, new AdrValidationOptions(AdrFormat.Canonical, ValidateMetadata: true));

        Assert.True(result.IsValid);
        Assert.Equal("R&D", document.DecisionMetadata?.Owner);
        Assert.Equal("Platform!", document.DecisionMetadata?.Category);
        Assert.Empty(document.MetadataErrors ?? []);
    }

    [Theory]
    [InlineData("owner: &anchor")]
    [InlineData("owner: *alias")]
    [InlineData("owner: !custom-tag value")]
    public void LeadingUnquotedYamlIndicatorsRemainUnsupported(string field)
    {
        var markdown = """
            ---
            owner: Architecture
            ---
            # Decision
            ## Status
            Accepted
            ## Context
            Context.
            ## Decision
            Decision.
            ## Consequences
            Consequences.
            """.Replace("owner: Architecture", field, StringComparison.Ordinal);
        var document = AdrMarkdownParser.Parse("0001-decision.md", markdown);

        var result = AdrValidator.Validate(
            [document], null, new AdrValidationOptions(AdrFormat.Canonical, ValidateMetadata: true));

        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.InvalidMetadata);
    }

    [Fact]
    public void DuplicateAndMalformedMetadataOnlyFailWhenPolicyIsEnabled()
    {
        var document = AdrMarkdownParser.Parse("0001-decision.md", """
            ---
            date: yesterday
            date: 2026-10-09
            owner: &unsafe
            ---
            # Decision
            ## Status
            Accepted
            ## Context
            Context.
            ## Decision
            Decision.
            ## Consequences
            Consequences.
            """);
        Assert.True(AdrValidator.Validate([document]).IsValid);
        var result = AdrValidator.Validate([document], null, new AdrValidationOptions(AdrFormat.Canonical, ValidateMetadata: true));
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == ValidationCodes.InvalidMetadata);
    }
}
