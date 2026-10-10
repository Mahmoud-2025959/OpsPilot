namespace OpsPilot.Domain.Enums;

/// <summary>
/// The kind of AI output stored as a suggestion. Suggestions are kept separate from
/// the facts on an incident so that machine output is never mistaken for recorded truth.
/// </summary>
public enum AiSuggestionKind
{
    Summary,
    RootCause,
    Remediation,
    Runbook
}
