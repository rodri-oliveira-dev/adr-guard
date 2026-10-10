using AdrGuard.Generation;
using AdrGuard.Parsing;
using AdrGuard.Validation;
using Xunit;

namespace AdrGuard.Tests.Validation;

public sealed class AdrFilenamePolicyTests
{
    [Theory]
    [InlineData("unnumbered", "choose-database.md", null, "choose-database")]
    [InlineData("adr-prefix", "ADR-0007-choose-database.md", 7, "choose-database")]
    [InlineData("numeric:6", "000007-choose-database.md", 7, "choose-database")]
    public void OptInPoliciesParseAndValidate(string policyName, string fileName, int? id, string slug)
    {
        var document = AdrMarkdownParser.Parse(fileName, Canonical);
        var options = new AdrValidationOptions(
            AdrFormat.Canonical,
            FilenamePolicy: AdrFilenamePolicy.Parse(policyName));

        Assert.Equal(id, document.Id);
        Assert.Equal(slug, document.Slug);
        Assert.True(AdrValidator.Validate([document], null, options).IsValid);
    }

    [Fact]
    public void DefaultAllocationAndOptInFormattingAreDeterministic()
    {
        Assert.EndsWith("0001-use-cache.md", AdrCreationService.AllocateFilePath(".", "Use Cache", []), StringComparison.Ordinal);
        Assert.EndsWith("ADR-0001-use-cache.md", AdrCreationService.AllocateFilePath(".", "Use Cache", [], AdrFilenamePolicy.Parse("adr-prefix")), StringComparison.Ordinal);
        Assert.EndsWith("use-cache.md", AdrCreationService.AllocateFilePath(".", "Use Cache", [], AdrFilenamePolicy.Parse("unnumbered")), StringComparison.Ordinal);
    }

    [Fact]
    public void UnnumberedIdentityCollisionsAreRejectedCaseInsensitively()
    {
        var first = AdrMarkdownParser.Parse("choose-database.md", Canonical);
        var second = AdrMarkdownParser.Parse(Path.Combine("nested", "choose-database.md"), Canonical);
        var options = new AdrValidationOptions(AdrFormat.Canonical, FilenamePolicy: AdrFilenamePolicy.Parse("unnumbered"));

        var result = AdrValidator.Validate([first, second], null, options);

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == ValidationCodes.DuplicateId));
    }

    [Theory]
    [InlineData("2026-plan.md", 2026)]
    [InlineData("9999-roadmap.md", 9999)]
    public void UnnumberedPolicyTreatsNumericLeadingStemsAsSlugs(string fileName, int parsedId)
    {
        var document = AdrMarkdownParser.Parse(fileName, Canonical);
        Assert.Equal(parsedId, document.Id); // Legacy parsing remains unchanged.

        var policy = AdrFilenamePolicy.Parse("unnumbered");
        var normalized = policy.NormalizeIdentity(document);
        Assert.Null(normalized.Id);
        Assert.Equal(Path.GetFileNameWithoutExtension(fileName), normalized.Slug);
        Assert.Equal($"slug:{normalized.Slug}", normalized.StableId);
        Assert.True(AdrValidator.Validate(
            [document], null, new AdrValidationOptions(AdrFormat.Canonical, FilenamePolicy: policy)).IsValid);
    }

    [Fact]
    public void NumericLeadingUnnumberedStemsUseStableSlugCollisionDetection()
    {
        var first = AdrMarkdownParser.Parse("2026-plan.md", Canonical);
        var duplicate = AdrMarkdownParser.Parse(Path.Combine("nested", "2026-plan.md"), Canonical);
        var policy = AdrFilenamePolicy.Parse("unnumbered");

        var result = AdrValidator.Validate(
            [first, duplicate], null, new AdrValidationOptions(AdrFormat.Canonical, FilenamePolicy: policy));

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == ValidationCodes.DuplicateId));
    }

    private const string Canonical = """
        # Choose database
        ## Status
        Accepted
        ## Context
        Context.
        ## Decision
        Decision.
        ## Consequences
        Consequences.
        """;
}
