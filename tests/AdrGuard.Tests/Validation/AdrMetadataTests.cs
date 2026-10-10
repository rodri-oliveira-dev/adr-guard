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
