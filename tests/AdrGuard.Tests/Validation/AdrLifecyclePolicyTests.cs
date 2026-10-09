using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrLifecyclePolicyTests
{
    [Fact]
    public void RejectedIsOptIn()
    {
        var document = Parse("0001-rejected.md", "Rejected");
        Assert.Contains(AdrValidator.Validate([document]).Issues, issue => issue.Code == ValidationCodes.InvalidStatus);

        var options = new AdrValidationOptions(
            AdrFormat.Canonical,
            Lifecycle: AdrLifecyclePolicy.Parse("Rejected=rejected"));

        Assert.True(AdrValidator.Validate([document], null, options).IsValid);
    }

    [Fact]
    public void ConfiguredAliasUsesExplicitSemanticKind()
    {
        var document = Parse("0001-review.md", "Under Review");
        var options = new AdrValidationOptions(
            AdrFormat.Canonical,
            Lifecycle: AdrLifecyclePolicy.Parse("Under Review=proposed"));
        Assert.True(AdrValidator.Validate([document], null, options).IsValid);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("=rejected")]
    [InlineData("Rejected=unknown")]
    [InlineData("Accepted=accepted")]
    public void MalformedOrConflictingPolicyIsRejected(string specification) =>
        Assert.Throws<ArgumentException>(() => AdrLifecyclePolicy.Parse(specification));

    private static AdrGuard.Model.AdrDocument Parse(string name, string status) =>
        AdrMarkdownParser.Parse(name, $$"""
            # Decision
            ## Status
            {{status}}
            ## Context
            Context.
            ## Decision
            Decision.
            ## Consequences
            Consequences.
            """);
}
