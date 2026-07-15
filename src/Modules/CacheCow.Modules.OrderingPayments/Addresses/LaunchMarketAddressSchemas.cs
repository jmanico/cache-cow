using System.Text.RegularExpressions;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Addresses;

/// <summary>
/// The per-market address schemas for the six launch markets (CC-MKT-001;
/// CC-ORD-002), declared as data (issue 038: no scattered conditionals).
///
/// CC-ORD-002 names exactly two concrete validations — Japanese address
/// structure and Indian PIN codes — and is otherwise silent. Everything else
/// here is deliberately conservative (presence of required lines, length
/// bounds, digit-format postal codes) and remains OPEN per issue 038's Open
/// Questions until humans ratify the full per-market rule sets:
/// - US: ZIP accepted as 5-digit or ZIP+4; whether ZIP+4 should be captured
///   separately is undecided.
/// - ES/MX: 5-digit postal codes; required-field sets beyond that unratified.
/// - DE: 5-digit PLZ; the street line must contain a house number digit
///   (Straße + Hausnummer convention) — a conservative reading, unratified.
/// - JP: field inventory (postal code, prefecture, municipality,
///   chōme-banchi-gō, building/room) awaits ratification.
/// - External deliverability verification is NOT performed — no vendor is
///   ratified for address verification (issue 038, Open Questions).
///
/// Patterns use explicit ASCII digit classes (never <c>\d</c>, which matches
/// Unicode digits) and are fully anchored: reject, never coerce
/// (SECURITY.md, Input validation rule 1).
/// </summary>
public static partial class LaunchMarketAddressSchemas
{
    private const int NameMaxLength = 100;
    private const int LineMaxLength = 200;
    private const int LocalityMaxLength = 100;
    private const int PostalCodeMaxLength = 12;

    /// <summary>One schema per launch market — the complete, ready-to-register set.</summary>
    public static IReadOnlyList<MarketAddressSchema> All { get; } =
    [
        Us(),
        Es(),
        Mx(),
        De(),
        Jp(),
        In(),
    ];

    [GeneratedRegex("^[0-9]{5}(-[0-9]{4})?$")]
    private static partial Regex UsZipPattern();

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex FiveDigitPostalCodePattern();

    [GeneratedRegex("^[0-9]{3}-[0-9]{4}$")]
    private static partial Regex JpPostalCodePattern();

    [GeneratedRegex("^[1-9][0-9]{5}$")]
    private static partial Regex InPinCodePattern();

    /// <summary>Conservative Straße + Hausnummer rule: the DE street line must contain a digit.</summary>
    [GeneratedRegex("[0-9]")]
    private static partial Regex ContainsDigitPattern();

    private static MarketAddressSchema Us() => new(
        Market.US,
        [
            new AddressFieldRule(AddressFieldNames.RecipientName, Required: true, NameMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.StreetAddress, Required: true, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.SecondaryAddress, Required: false, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.City, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.State, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.ZipCode, Required: true, PostalCodeMaxLength, UsZipPattern()),
        ],
        fields => new UsDeliveryAddress(
            fields.Required(AddressFieldNames.RecipientName),
            fields.Required(AddressFieldNames.StreetAddress),
            fields.Optional(AddressFieldNames.SecondaryAddress),
            fields.Required(AddressFieldNames.City),
            fields.Required(AddressFieldNames.State),
            fields.Required(AddressFieldNames.ZipCode)));

    private static MarketAddressSchema Es() => new(
        Market.ES,
        [
            new AddressFieldRule(AddressFieldNames.RecipientName, Required: true, NameMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.StreetAddress, Required: true, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.SecondaryAddress, Required: false, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.City, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.Province, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.PostalCode, Required: true, PostalCodeMaxLength, FiveDigitPostalCodePattern()),
        ],
        fields => new EsDeliveryAddress(
            fields.Required(AddressFieldNames.RecipientName),
            fields.Required(AddressFieldNames.StreetAddress),
            fields.Optional(AddressFieldNames.SecondaryAddress),
            fields.Required(AddressFieldNames.City),
            fields.Required(AddressFieldNames.Province),
            fields.Required(AddressFieldNames.PostalCode)));

    private static MarketAddressSchema Mx() => new(
        Market.MX,
        [
            new AddressFieldRule(AddressFieldNames.RecipientName, Required: true, NameMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.StreetAddress, Required: true, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.SecondaryAddress, Required: false, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.City, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.State, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.PostalCode, Required: true, PostalCodeMaxLength, FiveDigitPostalCodePattern()),
        ],
        fields => new MxDeliveryAddress(
            fields.Required(AddressFieldNames.RecipientName),
            fields.Required(AddressFieldNames.StreetAddress),
            fields.Optional(AddressFieldNames.SecondaryAddress),
            fields.Required(AddressFieldNames.City),
            fields.Required(AddressFieldNames.State),
            fields.Required(AddressFieldNames.PostalCode)));

    private static MarketAddressSchema De() => new(
        Market.DE,
        [
            new AddressFieldRule(AddressFieldNames.RecipientName, Required: true, NameMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.StreetAddress, Required: true, LineMaxLength, ContainsDigitPattern()),
            new AddressFieldRule(AddressFieldNames.SecondaryAddress, Required: false, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.City, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.PostalCode, Required: true, PostalCodeMaxLength, FiveDigitPostalCodePattern()),
        ],
        fields => new DeDeliveryAddress(
            fields.Required(AddressFieldNames.RecipientName),
            fields.Required(AddressFieldNames.StreetAddress),
            fields.Optional(AddressFieldNames.SecondaryAddress),
            fields.Required(AddressFieldNames.City),
            fields.Required(AddressFieldNames.PostalCode)));

    private static MarketAddressSchema Jp() => new(
        Market.JP,
        [
            new AddressFieldRule(AddressFieldNames.RecipientName, Required: true, NameMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.PostalCode, Required: true, PostalCodeMaxLength, JpPostalCodePattern()),
            new AddressFieldRule(AddressFieldNames.Prefecture, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.Municipality, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.ChomeBanchiGo, Required: true, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.BuildingAndRoom, Required: false, LineMaxLength, Pattern: null),
        ],
        fields => new JpDeliveryAddress(
            fields.Required(AddressFieldNames.RecipientName),
            fields.Required(AddressFieldNames.PostalCode),
            fields.Required(AddressFieldNames.Prefecture),
            fields.Required(AddressFieldNames.Municipality),
            fields.Required(AddressFieldNames.ChomeBanchiGo),
            fields.Optional(AddressFieldNames.BuildingAndRoom)));

    private static MarketAddressSchema In() => new(
        Market.IN,
        [
            new AddressFieldRule(AddressFieldNames.RecipientName, Required: true, NameMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.StreetAddress, Required: true, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.SecondaryAddress, Required: false, LineMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.City, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.State, Required: true, LocalityMaxLength, Pattern: null),
            new AddressFieldRule(AddressFieldNames.PinCode, Required: true, PostalCodeMaxLength, InPinCodePattern()),
        ],
        fields => new InDeliveryAddress(
            fields.Required(AddressFieldNames.RecipientName),
            fields.Required(AddressFieldNames.StreetAddress),
            fields.Optional(AddressFieldNames.SecondaryAddress),
            fields.Required(AddressFieldNames.City),
            fields.Required(AddressFieldNames.State),
            fields.Required(AddressFieldNames.PinCode)));
}
