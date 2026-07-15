using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Addresses;

/// <summary>
/// A validated, structured delivery address (issue 038; CC-ORD-002). Instances
/// exist only as output of <see cref="AddressValidator"/> — the constructors
/// are internal, so an unvalidated address is unrepresentable downstream
/// (SECURITY.md, Input validation rule 1). Structured fields, never a
/// free-text blob, so fulfillment routing (CC-FUL-001) and serviceability
/// checks (CC-FUL-002) can key on discrete fields.
///
/// Addresses are Restricted/PII (CC-CMP-001/002): <see cref="ToString"/> is
/// sealed and redacting so an address can never leak into a log message or
/// exception by accidental formatting (SECURITY.md, Logging rules 4–5, 8).
/// </summary>
public abstract class DeliveryAddress
{
    private protected DeliveryAddress(Market market, string recipientName)
    {
        Market = market;
        RecipientName = recipientName;
    }

    /// <summary>The transacting market whose schema validated this address (server-resolved, CC-SEC-012).</summary>
    public Market Market { get; }

    public string RecipientName { get; }

    /// <summary>Redacting on purpose: delivery addresses are PII (SECURITY.md, Logging rule 4).</summary>
    public sealed override string ToString() => GetType().Name + " [PII redacted]";
}

/// <summary>US delivery address (CC-ORD-002).</summary>
public sealed class UsDeliveryAddress : DeliveryAddress
{
    internal UsDeliveryAddress(
        string recipientName,
        string streetAddress,
        string? secondaryAddress,
        string city,
        string state,
        string zipCode)
        : base(Market.US, recipientName)
    {
        StreetAddress = streetAddress;
        SecondaryAddress = secondaryAddress;
        City = city;
        State = state;
        ZipCode = zipCode;
    }

    public string StreetAddress { get; }

    public string? SecondaryAddress { get; }

    public string City { get; }

    public string State { get; }

    /// <summary>5-digit ZIP or ZIP+4 (whether ZIP+4 stays accepted is an open question, issue 038).</summary>
    public string ZipCode { get; }
}

/// <summary>ES delivery address (CC-ORD-002).</summary>
public sealed class EsDeliveryAddress : DeliveryAddress
{
    internal EsDeliveryAddress(
        string recipientName,
        string streetAddress,
        string? secondaryAddress,
        string city,
        string province,
        string postalCode)
        : base(Market.ES, recipientName)
    {
        StreetAddress = streetAddress;
        SecondaryAddress = secondaryAddress;
        City = city;
        Province = province;
        PostalCode = postalCode;
    }

    public string StreetAddress { get; }

    public string? SecondaryAddress { get; }

    public string City { get; }

    public string Province { get; }

    public string PostalCode { get; }
}

/// <summary>MX delivery address (CC-ORD-002).</summary>
public sealed class MxDeliveryAddress : DeliveryAddress
{
    internal MxDeliveryAddress(
        string recipientName,
        string streetAddress,
        string? secondaryAddress,
        string city,
        string state,
        string postalCode)
        : base(Market.MX, recipientName)
    {
        StreetAddress = streetAddress;
        SecondaryAddress = secondaryAddress;
        City = city;
        State = state;
        PostalCode = postalCode;
    }

    public string StreetAddress { get; }

    /// <summary>Optional colonia/interior line.</summary>
    public string? SecondaryAddress { get; }

    public string City { get; }

    public string State { get; }

    public string PostalCode { get; }
}

/// <summary>DE delivery address: Straße + Hausnummer on one street line (CC-ORD-002).</summary>
public sealed class DeDeliveryAddress : DeliveryAddress
{
    internal DeDeliveryAddress(
        string recipientName,
        string streetAddress,
        string? secondaryAddress,
        string city,
        string postalCode)
        : base(Market.DE, recipientName)
    {
        StreetAddress = streetAddress;
        SecondaryAddress = secondaryAddress;
        City = city;
        PostalCode = postalCode;
    }

    /// <summary>Street with house number (schema requires a digit — conservative rule, issue 038 Open Questions).</summary>
    public string StreetAddress { get; }

    public string? SecondaryAddress { get; }

    public string City { get; }

    /// <summary>5-digit PLZ.</summary>
    public string PostalCode { get; }
}

/// <summary>
/// JP delivery address in structured Japanese form — postal code NNN-NNNN,
/// prefecture / municipality / chōme-banchi-gō levels — not a generic
/// street/city template (CC-ORD-002, issue 038 AC-02). The exact field
/// inventory awaits ratification (issue 038, Open Questions).
/// </summary>
public sealed class JpDeliveryAddress : DeliveryAddress
{
    internal JpDeliveryAddress(
        string recipientName,
        string postalCode,
        string prefecture,
        string municipality,
        string chomeBanchiGo,
        string? buildingAndRoom)
        : base(Market.JP, recipientName)
    {
        PostalCode = postalCode;
        Prefecture = prefecture;
        Municipality = municipality;
        ChomeBanchiGo = chomeBanchiGo;
        BuildingAndRoom = buildingAndRoom;
    }

    /// <summary>NNN-NNNN.</summary>
    public string PostalCode { get; }

    /// <summary>都道府県.</summary>
    public string Prefecture { get; }

    /// <summary>市区町村.</summary>
    public string Municipality { get; }

    /// <summary>丁目・番地・号 area/block line.</summary>
    public string ChomeBanchiGo { get; }

    /// <summary>Optional building name and room number.</summary>
    public string? BuildingAndRoom { get; }
}

/// <summary>IN delivery address with a 6-digit PIN code, first digit non-zero (CC-ORD-002).</summary>
public sealed class InDeliveryAddress : DeliveryAddress
{
    internal InDeliveryAddress(
        string recipientName,
        string streetAddress,
        string? secondaryAddress,
        string city,
        string state,
        string pinCode)
        : base(Market.IN, recipientName)
    {
        StreetAddress = streetAddress;
        SecondaryAddress = secondaryAddress;
        City = city;
        State = state;
        PinCode = pinCode;
    }

    public string StreetAddress { get; }

    public string? SecondaryAddress { get; }

    public string City { get; }

    public string State { get; }

    /// <summary>6 digits, first digit non-zero.</summary>
    public string PinCode { get; }
}
