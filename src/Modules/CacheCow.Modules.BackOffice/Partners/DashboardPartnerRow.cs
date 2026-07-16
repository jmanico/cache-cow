using System.Collections.ObjectModel;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Partners;

/// <summary>
/// The dashboard's read-model view of partner onboarding state (CC-WHS-002).
/// A MIRROR of the Wholesale &amp; B2B context's own state enum, deliberately
/// duplicated rather than referenced: modules reference only the shared kernel
/// (ARCHITECTURE.md, Dependency rule 9), and partner tenancy is not shared
/// kernel. The host adapter maps between the two.
///
/// Display and request vocabulary ONLY — workflow legality (which transitions
/// exist, and from where) is enforced exclusively by the Wholesale context's
/// onboarding workflow behind <see cref="IDashboardPartnerWorkflow"/>. Only
/// <see cref="Approved"/> is an active partner (CC-WHS-002: no self-service
/// activation).
/// </summary>
public enum DashboardPartnerState
{
    /// <summary>Record created; business-identity capture in progress.</summary>
    Draft = 0,

    /// <summary>Submitted for approval — the pending state an approver acts on.</summary>
    Submitted = 1,

    /// <summary>The single active state, reachable only via the audited dashboard approval.</summary>
    Approved = 2,

    /// <summary>Approval declined.</summary>
    Rejected = 3,

    /// <summary>Deactivated after approval.</summary>
    Suspended = 4,
}

/// <summary>
/// One per-market business identity of a partner (CC-WHS-002: "business
/// identity captured per market (e.g., USt-IdNr. in DE, GSTIN in IN)").
///
/// The identity KIND travels as a supplied string rather than an enum, because
/// CC-WHS-002 gives DE and IN only as examples and the canonical documents
/// enumerate no identity field for US, ES, MX, or JP (issue 085, Open
/// Questions). A closed enum here would silently invent that list; the
/// Wholesale context — which captures and validates these — supplies the kind
/// it recorded.
/// </summary>
public sealed record DashboardPartnerIdentity
{
    /// <summary>Maximum length of the identity kind and value.</summary>
    public const int MaxFieldLength = 128;

    private DashboardPartnerIdentity(Market market, string kind, string value)
    {
        Market = market;
        Kind = kind;
        Value = value;
    }

    /// <summary>The market this identity is registered in (CC-MKT-001).</summary>
    public Market Market { get; }

    /// <summary>What kind of identifier this is, as recorded by the Wholesale context (e.g. "USt-IdNr.", "GSTIN").</summary>
    public string Kind { get; }

    /// <summary>The identifier itself. Confidential business data (issue 085, Data Classification).</summary>
    public string Value { get; }

    /// <exception cref="DashboardValidationException">Any field is invalid.</exception>
    public static DashboardPartnerIdentity Create(Market market, string kind, string value)
    {
        if (market == default)
        {
            throw new DashboardValidationException(
                "A partner business identity requires an initialized launch market (CC-MKT-001).");
        }

        DashboardPartnerFields.ValidateBounded(kind, nameof(kind), MaxFieldLength);
        DashboardPartnerFields.ValidateBounded(value, nameof(value), MaxFieldLength);

        return new DashboardPartnerIdentity(market, kind, value);
    }
}

/// <summary>
/// One partner row for the dashboard's partner-management list (issue 085,
/// AC-01; CC-DSH-003, CC-WHS-002).
///
/// PII-MINIMAL, exactly as the order row is. Issue 085 classifies partner
/// CONTACT details as Restricted/PII while the business identity and terms are
/// Confidential; a searchable list needs neither contact names nor email
/// addresses, so none are representable on this type. Contact data, if a role
/// ever needs it, is a separate and separately-authorized surface — this
/// keeps partner PII out of grids, logs, and audit summaries by construction
/// (issue 085, Anti-Patterns: never log partner contact PII un-redacted).
/// </summary>
public sealed record DashboardPartnerRow
{
    /// <summary>Maximum length of a partner identifier.</summary>
    public const int MaxPartnerIdLength = 64;

    /// <summary>Maximum length of a partner's legal name.</summary>
    public const int MaxLegalNameLength = 256;

    private DashboardPartnerRow(string partnerId, string legalName, DashboardPartnerState state)
    {
        PartnerId = partnerId;
        LegalName = legalName;
        State = state;
    }

    /// <summary>The partner's identity in the Wholesale context (monospace in the UI per DESIGN.md §12).</summary>
    public string PartnerId { get; }

    /// <summary>The partner's registered business name — a company, not a person (CC-WHS-002).</summary>
    public string LegalName { get; }

    /// <summary>Current onboarding state, as reported by the Wholesale context.</summary>
    public DashboardPartnerState State { get; }

    /// <exception cref="DashboardValidationException">Any field is invalid.</exception>
    public static DashboardPartnerRow Create(string partnerId, string legalName, DashboardPartnerState state)
    {
        ValidatePartnerId(partnerId);
        DashboardPartnerFields.ValidateBounded(legalName, nameof(legalName), MaxLegalNameLength);

        if (!Enum.IsDefined(state))
        {
            throw new DashboardValidationException(
                $"Partner state {(int)state} is outside the CC-WHS-002 closed set; rejected (SECURITY.md, Input validation rule 1).");
        }

        return new DashboardPartnerRow(partnerId, legalName, state);
    }

    /// <summary>
    /// Validates a partner identifier: required, bounded, control-character
    /// free (SECURITY.md, Logging rule 5). Rejected, never coerced.
    /// </summary>
    public static void ValidatePartnerId(string partnerId) =>
        DashboardPartnerFields.ValidateBounded(partnerId, nameof(partnerId), MaxPartnerIdLength);
}

/// <summary>
/// A partner's detail view (issue 085, AC-01/AC-04): the row plus per-market
/// business identities and the payment terms.
/// </summary>
public sealed record DashboardPartnerDetail
{
    private DashboardPartnerDetail(
        DashboardPartnerRow summary,
        IReadOnlyList<DashboardPartnerIdentity> identities,
        int paymentTermsNetDays)
    {
        Summary = summary;
        Identities = identities;
        PaymentTermsNetDays = paymentTermsNetDays;
    }

    /// <summary>The list-level fields.</summary>
    public DashboardPartnerRow Summary { get; }

    /// <summary>Per-market business identities captured at onboarding (CC-WHS-002).</summary>
    public IReadOnlyList<DashboardPartnerIdentity> Identities { get; }

    /// <summary>
    /// The partner's wholesale payment terms in days (CC-WHS-004: net-60
    /// default, ratified 2026-07-15, adjustable per partner). An integer count
    /// of days — never money, never a rate.
    ///
    /// DISPLAY ONLY on this surface. The default and the per-partner override
    /// are the Wholesale context's data (issue 050); this module shows what
    /// that context reports. No terms-ADJUSTMENT action exists here — see
    /// <see cref="IDashboardPartnerWorkflow"/>.
    /// </summary>
    public int PaymentTermsNetDays { get; }

    /// <exception cref="DashboardValidationException">Any field is invalid.</exception>
    public static DashboardPartnerDetail Create(
        DashboardPartnerRow summary,
        IReadOnlyList<DashboardPartnerIdentity> identities,
        int paymentTermsNetDays)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(identities);

        if (paymentTermsNetDays < 0)
        {
            throw new DashboardValidationException(
                "Partner payment terms must not be a negative number of days (CC-WHS-004).");
        }

        return new DashboardPartnerDetail(
            summary,
            new ReadOnlyCollection<DashboardPartnerIdentity>([.. identities]),
            paymentTermsNetDays);
    }
}

/// <summary>Shared field validation for the partner read model.</summary>
internal static class DashboardPartnerFields
{
    /// <summary>Required, bounded, and free of control characters (SECURITY.md, Logging rule 5).</summary>
    internal static void ValidateBounded(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            throw new DashboardValidationException(
                $"Partner field '{fieldName}' is required and at most {maxLength} characters (SECURITY.md, Input validation rule 1).");
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                throw new DashboardValidationException(
                    $"Partner field '{fieldName}' must not contain control characters (SECURITY.md, Logging rule 5).");
            }
        }
    }
}
