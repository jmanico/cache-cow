using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// A partner's legal business identity in one market, captured during
/// onboarding and schema-validated server-side (CC-WHS-002, AC-03; SECURITY.md,
/// Input validation rule 1 — reject, never coerce). Per-market rules:
///
/// - DE: USt-IdNr., exactly "DE" followed by 9 digits (EU VAT invoicing,
///   CC-INV-001).
/// - IN: GSTIN, 15 characters in the published structure (2-digit state code,
///   10-character PAN, entity code, the literal 'Z', check character). The
///   format is validated conservatively: the mod-36 check-digit algorithm is
///   not computed because no canonical document ratifies it (issue 049 flags
///   only "schema-validated"); tightening to full checksum verification is an
///   open follow-up.
/// - US, ES, MX, JP: required fields are not enumerated by CC-WHS-002
///   (issue 049, Open Questions), so a non-empty opaque identifier is required
///   and no format is imposed until a human decision lands.
///
/// Values are Confidential (issue 049, Data Classification):
/// <see cref="ToString"/> redacts, and validation failures never echo the
/// submitted value (SECURITY.md, Logging rules 1 and 4).
/// </summary>
public sealed class BusinessIdentity
{
    private const int MaxOpaqueIdentifierLength = 128;

    private BusinessIdentity(Market market, string value)
    {
        Market = market;
        Value = value;
    }

    /// <summary>The market whose legal regime this identity satisfies (CC-MKT-001).</summary>
    public Market Market { get; }

    /// <summary>The validated registration identifier (Confidential; see class docs).</summary>
    public string Value { get; }

    public static BusinessIdentity Create(Market market, string value)
    {
        if (market == default)
        {
            throw new WholesaleValidationException(
                "A business identity requires a launch market (CC-WHS-002, CC-MKT-001).");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidBusinessIdentityException(
                $"A {market.Code} business identity requires a non-empty identifier (CC-WHS-002).");
        }

        if (market == Market.DE)
        {
            RequireUstIdNr(value);
        }
        else if (market == Market.IN)
        {
            RequireGstin(value);
        }
        else if (value.Length > MaxOpaqueIdentifierLength || value.Trim().Length != value.Length)
        {
            throw new InvalidBusinessIdentityException(
                $"A {market.Code} business identifier must be at most {MaxOpaqueIdentifierLength} characters with no surrounding whitespace; its exact format is an open decision (issue 049, Open Questions).");
        }

        return new BusinessIdentity(market, value);
    }

    /// <summary>Redacted: tax registration numbers never ride into logs or error bodies (SECURITY.md, Logging rule 4).</summary>
    public override string ToString() => $"{Market.Code} business identity (value redacted)";

    private static void RequireUstIdNr(string value)
    {
        var valid = value.Length == 11
            && value[0] == 'D'
            && value[1] == 'E';

        for (var i = 2; valid && i < value.Length; i++)
        {
            valid = char.IsAsciiDigit(value[i]);
        }

        if (!valid)
        {
            throw new InvalidBusinessIdentityException(
                "A DE business identity must be a USt-IdNr.: the literal 'DE' followed by exactly 9 digits (CC-WHS-002, AC-03; CC-INV-001).");
        }
    }

    private static void RequireGstin(string value)
    {
        var valid = value.Length == 15
            && char.IsAsciiDigit(value[0])            // state code
            && char.IsAsciiDigit(value[1])
            && char.IsAsciiLetterUpper(value[2])      // PAN: 5 letters
            && char.IsAsciiLetterUpper(value[3])
            && char.IsAsciiLetterUpper(value[4])
            && char.IsAsciiLetterUpper(value[5])
            && char.IsAsciiLetterUpper(value[6])
            && char.IsAsciiDigit(value[7])            // PAN: 4 digits
            && char.IsAsciiDigit(value[8])
            && char.IsAsciiDigit(value[9])
            && char.IsAsciiDigit(value[10])
            && char.IsAsciiLetterUpper(value[11])     // PAN: 1 letter
            && (char.IsBetween(value[12], '1', '9') || char.IsAsciiLetterUpper(value[12])) // entity code, never '0'
            && value[13] == 'Z'                       // fixed per the published format
            && (char.IsAsciiDigit(value[14]) || char.IsAsciiLetterUpper(value[14])); // check character (structure only; see class docs)

        if (!valid)
        {
            throw new InvalidBusinessIdentityException(
                "An IN business identity must be a GSTIN: 15 characters as 2-digit state code, 10-character PAN (5 letters, 4 digits, 1 letter), entity code, 'Z', check character (CC-WHS-002, AC-03; CC-INV-001). The check digit is validated structurally only — the checksum algorithm is an open follow-up.");
        }
    }
}
