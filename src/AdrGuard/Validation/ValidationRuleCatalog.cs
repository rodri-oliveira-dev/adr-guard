namespace AdrGuard.Validation;

internal static class ValidationRuleCatalog
{
    internal static string GetDescription(string code) => code switch
    {
        ValidationCodes.InvalidFileName => "ADR file name is invalid",
        ValidationCodes.MissingTitle => "ADR title is missing",
        ValidationCodes.MissingStatus => "ADR status is missing",
        ValidationCodes.InvalidStatus => "ADR status is invalid",
        ValidationCodes.MissingSection => "Required ADR section is missing",
        ValidationCodes.DuplicateId => "ADR identifier is duplicated",
        ValidationCodes.BrokenReference => "ADR reference is broken",
        ValidationCodes.MissingSupersededBy => "Superseded ADR target is missing",
        ValidationCodes.DuplicateCanonicalSection => "Canonical ADR section is duplicated",
        _ => "ADR validation diagnostic",
    };
}
