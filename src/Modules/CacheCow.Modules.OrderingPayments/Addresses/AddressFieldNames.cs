namespace CacheCow.Modules.OrderingPayments.Addresses;

/// <summary>
/// The closed vocabulary of address field names used by the per-market
/// address schemas (issue 038; CC-ORD-002). Field names are exact and
/// case-sensitive: a submission field not declared by the transacting
/// market's schema is rejected as unknown, never silently dropped
/// (SECURITY.md, Input validation rules 1–2).
/// </summary>
public static class AddressFieldNames
{
    public const string RecipientName = "recipientName";

    /// <summary>Street line including house/building number (per-market conventions, e.g. DE Straße + Hausnummer).</summary>
    public const string StreetAddress = "streetAddress";

    /// <summary>Optional second line (apartment, unit, colonia, Adresszusatz).</summary>
    public const string SecondaryAddress = "secondaryAddress";

    public const string City = "city";

    /// <summary>US/MX/IN state.</summary>
    public const string State = "state";

    /// <summary>ES provincia.</summary>
    public const string Province = "province";

    /// <summary>ES/MX/DE/JP postal code (JP: NNN-NNNN).</summary>
    public const string PostalCode = "postalCode";

    /// <summary>US ZIP code.</summary>
    public const string ZipCode = "zipCode";

    /// <summary>IN 6-digit PIN code, first digit non-zero (CC-ORD-002).</summary>
    public const string PinCode = "pinCode";

    /// <summary>JP 都道府県 (todōfuken).</summary>
    public const string Prefecture = "prefecture";

    /// <summary>JP 市区町村 (shikuchōson).</summary>
    public const string Municipality = "municipality";

    /// <summary>JP 丁目・番地・号 area/block line.</summary>
    public const string ChomeBanchiGo = "chomeBanchiGo";

    /// <summary>JP optional building name and room.</summary>
    public const string BuildingAndRoom = "buildingAndRoom";
}
