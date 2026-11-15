namespace OpsPilot.Application.Common.Constants;

/// <summary>
/// The names the consumed-message ledger is written under.
/// </summary>
/// <remarks>
/// The name is part of the ledger's key, so it has to be stable: a rename would make the
/// messages already consumed look like messages nobody handled, and they would be applied a
/// second time.
/// </remarks>
public static class IntegrationConsumers
{
    /// <summary>The consumer that applies integration events arriving over the broker.</summary>
    public const string Inbound = "opspilot.integration.inbound";
}
