namespace AdrGuard.Validation;

internal static class ValidationCodes
{
    internal const string InvalidFileName = "ADR001";
    internal const string MissingTitle = "ADR002";
    internal const string MissingStatus = "ADR003";
    internal const string InvalidStatus = "ADR004";
    internal const string MissingSection = "ADR005";
    internal const string DuplicateId = "ADR006";
    internal const string BrokenReference = "ADR007";
    internal const string MissingSupersededBy = "ADR008";
    internal const string DuplicateCanonicalSection = "ADR009";
    internal const string SupersessionCycle = "ADR010";
    internal const string SelfSupersession = "ADR011";
    internal const string MultipleSuperseders = "ADR012";
    internal const string InconsistentSupersession = "ADR013";
    internal const string InactiveDependency = "ADR014";
    internal const string UnresolvedPlaceholder = "ADR015";
    internal const string InvalidMetadata = "ADR016";
}
