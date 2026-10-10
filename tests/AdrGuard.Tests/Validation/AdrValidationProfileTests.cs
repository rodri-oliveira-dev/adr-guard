using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrValidationProfileTests
{
    [Theory]
    [InlineData("canonical", "legacy")]
    [InlineData("canonical", "advisory")]
    [InlineData("canonical", "standard")]
    [InlineData("canonical", "strict")]
    [InlineData("madr-4", "legacy")]
    [InlineData("madr-4", "advisory")]
    public void FormatAndProfileAreIndependent(string formatName, string profile)
    {
        var format = formatName == "madr-4" ? AdrFormat.Madr4 : AdrFormat.Canonical;
        var options = AdrValidationOptionsFactory.Create(format, null, null, false, null, null, false, profile);
        Assert.Equal(format, options.Format);
        Assert.Equal(AdrValidationOptionsFactory.ParseProfile(profile), AdrValidationOptionsFactory.ParseProfile(profile));
    }

    [Fact]
    public void LegacyPreservesDefaultAndStrictEnablesObjectivePolicies()
    {
        var rejected = AdrMarkdownParser.Parse("0001-rejected.md", Canonical("Rejected", "TODO"));
        var legacy = AdrValidationOptionsFactory.Create(AdrFormat.Canonical, null, null, false, null, null, false, "legacy");
        var strict = AdrValidationOptionsFactory.Create(AdrFormat.Canonical, null, null, false, null, null, false, "strict");

        Assert.Contains(AdrValidator.Validate([rejected], null, legacy).Issues, issue => issue.Code == ValidationCodes.InvalidStatus);
        var strictResult = AdrValidator.Validate([rejected], null, strict);
        Assert.DoesNotContain(strictResult.Issues, issue => issue.Code == ValidationCodes.InvalidStatus);
        Assert.Contains(strictResult.Issues, issue => issue.Code == ValidationCodes.UnresolvedPlaceholder);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    public void InvalidProfileIsRejected(string profile) =>
        Assert.Throws<ArgumentException>(() => AdrValidationOptionsFactory.ParseProfile(profile));

    private static string Canonical(string status, string context) => $$"""
        # Decision
        ## Status
        {{status}}
        ## Context
        {{context}}
        ## Decision
        Decision.
        ## Consequences
        Consequences.
        """;
}
