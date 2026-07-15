namespace CacheCow.Modules.Invoicing.Tax;

/// <summary>
/// UNRATIFIED — fails closed by construction (issue 047, Open Questions; epic
/// open question 8). MX is a launch market with IVA-inclusive pricing
/// (CC-PRC-002) and Stripe Tax coverage (ARCHITECTURE.md), but CC-INV-001's
/// tax-element list (US, EU VAT, JP, IN) enumerates no MX invoice content
/// (e.g. IVA lines, CFDI). Per CLAUDE.md working rules this open decision is
/// surfaced, not resolved: no MX tax content instance can exist, so no MX
/// invoice can ever be issued until a human ratifies the MX invoice shape.
/// </summary>
public static class MexicoTaxContent
{
    /// <summary>
    /// Always throws <see cref="UnratifiedMarketTaxContentException"/>. Exists
    /// so the MX gap is an explicit, discoverable decision point rather than a
    /// silent omission.
    /// </summary>
    public static MarketTaxContent Compose() =>
        throw new UnratifiedMarketTaxContentException(
            "MX invoice tax content is not enumerated by CC-INV-001 (IVA lines? CFDI?) and is an open "
            + "decision awaiting a human (issue 047, Open Questions). MX invoice issuance fails closed.");
}

/// <summary>
/// An invoice was requested for a market whose legal tax content is not
/// ratified in the canonical specs. Issuance fails closed (CC-INV-001;
/// SECURITY.md, Logging rule 2); the decision is tracked as an issue-047 open
/// question, never resolved in code (CLAUDE.md working rules).
/// </summary>
public sealed class UnratifiedMarketTaxContentException : Exception
{
    public UnratifiedMarketTaxContentException(string message)
        : base(message)
    {
    }
}
