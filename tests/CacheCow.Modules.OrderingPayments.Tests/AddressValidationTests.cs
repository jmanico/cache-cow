using CacheCow.Modules.OrderingPayments.Addresses;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Issue 038 (CC-ORD-002): per-market address schemas validate server-side
/// against explicit, data-driven rules — JP structured Japanese addressing,
/// IN PIN codes, DE street/house-number, US/ES/MX conservative formats.
/// Invalid input is rejected, never sanitized into acceptance; unknown
/// markets and unknown fields fail closed; rejections never echo submitted
/// values (PII).
/// </summary>
[Requirement("CC-ORD-002")]
public sealed class AddressValidationTests
{
    private static AddressValidator Validator() => new(LaunchMarketAddressSchemas.All);

    private static Dictionary<string, string?> ValidFields(Market market) => market.Code switch
    {
        "US" => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AddressFieldNames.RecipientName] = "Jane Doe",
            [AddressFieldNames.StreetAddress] = "123 Smokehouse Ln",
            [AddressFieldNames.SecondaryAddress] = "Apt 4",
            [AddressFieldNames.City] = "Austin",
            [AddressFieldNames.State] = "TX",
            [AddressFieldNames.ZipCode] = "78701",
        },
        "ES" => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AddressFieldNames.RecipientName] = "María García",
            [AddressFieldNames.StreetAddress] = "Calle de la Brasa 7, 2ºB",
            [AddressFieldNames.City] = "Madrid",
            [AddressFieldNames.Province] = "Madrid",
            [AddressFieldNames.PostalCode] = "28001",
        },
        "MX" => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AddressFieldNames.RecipientName] = "José Hernández",
            [AddressFieldNames.StreetAddress] = "Av. Insurgentes Sur 601",
            [AddressFieldNames.SecondaryAddress] = "Col. Nápoles",
            [AddressFieldNames.City] = "Ciudad de México",
            [AddressFieldNames.State] = "CDMX",
            [AddressFieldNames.PostalCode] = "03810",
        },
        "DE" => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AddressFieldNames.RecipientName] = "Max Mustermann",
            [AddressFieldNames.StreetAddress] = "Rauchgasse 12",
            [AddressFieldNames.City] = "Berlin",
            [AddressFieldNames.PostalCode] = "10115",
        },
        "JP" => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AddressFieldNames.RecipientName] = "山田太郎",
            [AddressFieldNames.PostalCode] = "150-0002",
            [AddressFieldNames.Prefecture] = "東京都",
            [AddressFieldNames.Municipality] = "渋谷区",
            [AddressFieldNames.ChomeBanchiGo] = "渋谷2丁目21-1",
            [AddressFieldNames.BuildingAndRoom] = "渋谷ヒカリエ 5F",
        },
        "IN" => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [AddressFieldNames.RecipientName] = "Priya Sharma",
            [AddressFieldNames.StreetAddress] = "12 Smokehouse Road",
            [AddressFieldNames.City] = "New Delhi",
            [AddressFieldNames.State] = "Delhi",
            [AddressFieldNames.PinCode] = "110001",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(market)),
    };

    public static TheoryData<string> AllMarketCodes()
    {
        var data = new TheoryData<string>();
        foreach (var market in Market.All)
        {
            data.Add(market.Code);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllMarketCodes))]
    public void Valid_address_is_accepted_per_market_schema(string marketCode)
    {
        // AC-01: each launch market validates against its own explicit schema
        // and a valid address for the transacting market is accepted.
        var market = Market.Parse(marketCode);

        var address = Validator().Validate(new AddressSubmission(ValidFields(market)), market);

        Assert.Equal(market, address.Market);
    }

    [Fact]
    public void Each_market_materializes_its_own_typed_structured_address()
    {
        // AC-06: validated addresses are structured typed fields, never a blob.
        var validator = Validator();

        var us = Assert.IsType<UsDeliveryAddress>(
            validator.Validate(new AddressSubmission(ValidFields(Market.US)), Market.US));
        Assert.Equal("78701", us.ZipCode);
        Assert.Equal("TX", us.State);
        Assert.Equal("Apt 4", us.SecondaryAddress);

        var es = Assert.IsType<EsDeliveryAddress>(
            validator.Validate(new AddressSubmission(ValidFields(Market.ES)), Market.ES));
        Assert.Equal("Madrid", es.Province);
        Assert.Null(es.SecondaryAddress);

        var mx = Assert.IsType<MxDeliveryAddress>(
            validator.Validate(new AddressSubmission(ValidFields(Market.MX)), Market.MX));
        Assert.Equal("03810", mx.PostalCode);

        var de = Assert.IsType<DeDeliveryAddress>(
            validator.Validate(new AddressSubmission(ValidFields(Market.DE)), Market.DE));
        Assert.Equal("Rauchgasse 12", de.StreetAddress);

        var jp = Assert.IsType<JpDeliveryAddress>(
            validator.Validate(new AddressSubmission(ValidFields(Market.JP)), Market.JP));
        Assert.Equal("150-0002", jp.PostalCode);
        Assert.Equal("東京都", jp.Prefecture);
        Assert.Equal("渋谷区", jp.Municipality);
        Assert.Equal("渋谷2丁目21-1", jp.ChomeBanchiGo);

        var india = Assert.IsType<InDeliveryAddress>(
            validator.Validate(new AddressSubmission(ValidFields(Market.IN)), Market.IN));
        Assert.Equal("110001", india.PinCode);
    }

    [Theory]
    // US ZIP (5-digit or ZIP+4; anything else rejected)
    [InlineData("US", AddressFieldNames.ZipCode, "1234")]
    [InlineData("US", AddressFieldNames.ZipCode, "123456")]
    [InlineData("US", AddressFieldNames.ZipCode, "ABCDE")]
    [InlineData("US", AddressFieldNames.ZipCode, "78701-12")]
    [InlineData("US", AddressFieldNames.ZipCode, " 78701")]
    // ES/MX/DE 5-digit postal codes
    [InlineData("ES", AddressFieldNames.PostalCode, "2800")]
    [InlineData("ES", AddressFieldNames.PostalCode, "280011")]
    [InlineData("MX", AddressFieldNames.PostalCode, "0381O")]
    [InlineData("DE", AddressFieldNames.PostalCode, "1011")]
    [InlineData("DE", AddressFieldNames.PostalCode, "10115 ")]
    // DE street line must carry a house number (conservative Straße + Hausnummer rule)
    [InlineData("DE", AddressFieldNames.StreetAddress, "Rauchgasse")]
    // JP postal code NNN-NNNN, ASCII digits only
    [InlineData("JP", AddressFieldNames.PostalCode, "1500002")]
    [InlineData("JP", AddressFieldNames.PostalCode, "15-00002")]
    [InlineData("JP", AddressFieldNames.PostalCode, "150-002")]
    [InlineData("JP", AddressFieldNames.PostalCode, "１５０-０００２")]
    // IN PIN: 6 digits, first non-zero (CC-ORD-002)
    [InlineData("IN", AddressFieldNames.PinCode, "011001")]
    [InlineData("IN", AddressFieldNames.PinCode, "11001")]
    [InlineData("IN", AddressFieldNames.PinCode, "1100011")]
    [InlineData("IN", AddressFieldNames.PinCode, "11000a")]
    [InlineData("IN", AddressFieldNames.PinCode, "110 001")]
    public void Malformed_postal_and_format_fields_are_rejected(string marketCode, string field, string badValue)
    {
        // AC-03 and the per-market format matrix: format violations reject the
        // submission; nothing is trimmed or coerced into acceptance.
        var market = Market.Parse(marketCode);
        var fields = ValidFields(market);
        fields[field] = badValue;

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), market));

        var error = Assert.Single(rejection.Errors);
        Assert.Equal(field, error.Field);
        Assert.Equal(AddressRejectionCode.InvalidFormat, error.Code);
    }

    [Theory]
    [InlineData("US", AddressFieldNames.ZipCode)]
    [InlineData("US", AddressFieldNames.RecipientName)]
    [InlineData("ES", AddressFieldNames.Province)]
    [InlineData("MX", AddressFieldNames.State)]
    [InlineData("DE", AddressFieldNames.PostalCode)]
    [InlineData("JP", AddressFieldNames.Prefecture)]
    [InlineData("JP", AddressFieldNames.Municipality)]
    [InlineData("JP", AddressFieldNames.ChomeBanchiGo)]
    [InlineData("JP", AddressFieldNames.PostalCode)]
    [InlineData("IN", AddressFieldNames.PinCode)]
    [InlineData("IN", AddressFieldNames.StreetAddress)]
    public void Missing_required_fields_are_rejected(string marketCode, string missingField)
    {
        // AC-02/AC-04: schema-required fields must be present — the JP
        // structured levels are individually required, not a generic template.
        var market = Market.Parse(marketCode);
        var fields = ValidFields(market);
        fields.Remove(missingField);

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), market));

        var error = Assert.Single(rejection.Errors);
        Assert.Equal(missingField, error.Field);
        Assert.Equal(AddressRejectionCode.MissingRequiredField, error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_required_field_is_rejected_not_defaulted(string blank)
    {
        var fields = ValidFields(Market.US);
        fields[AddressFieldNames.City] = blank;

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), Market.US));

        var error = Assert.Single(rejection.Errors);
        Assert.Equal(AddressRejectionCode.MissingRequiredField, error.Code);
    }

    [Fact]
    public void Blank_optional_field_is_rejected_not_silently_dropped()
    {
        // Reject-not-sanitize: a submitted-but-blank optional field is an
        // error, never quietly discarded (SECURITY.md, Input validation rule 1).
        var fields = ValidFields(Market.US);
        fields[AddressFieldNames.SecondaryAddress] = "  ";

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), Market.US));

        var error = Assert.Single(rejection.Errors);
        Assert.Equal(AddressFieldNames.SecondaryAddress, error.Field);
        Assert.Equal(AddressRejectionCode.BlankValue, error.Code);
    }

    [Fact]
    public void Unknown_fields_are_rejected_never_ignored()
    {
        // AC-04: over-posted fields outside the market schema are rejected
        // (SECURITY.md, Input validation rule 2).
        var fields = ValidFields(Market.DE);
        fields["province"] = "Bayern"; // not a DE schema field
        fields["isAdmin"] = "true";

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), Market.DE));

        Assert.Equal(2, rejection.Errors.Count);
        Assert.All(rejection.Errors, error => Assert.Equal(AddressRejectionCode.UnknownField, error.Code));
    }

    [Fact]
    public void Field_names_match_case_sensitively_no_coercion()
    {
        var fields = ValidFields(Market.US);
        fields.Remove(AddressFieldNames.ZipCode);
        fields["ZipCode"] = "78701"; // wrong case: unknown field AND missing required

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), Market.US));

        Assert.Contains(rejection.Errors, e => e is { Field: "ZipCode", Code: AddressRejectionCode.UnknownField });
        Assert.Contains(
            rejection.Errors,
            e => e.Field == AddressFieldNames.ZipCode && e.Code == AddressRejectionCode.MissingRequiredField);
    }

    [Theory]
    [InlineData("123 Smokehouse Ln\nSecond line")]
    [InlineData("123 Smokehouse Ln\r\n")]
    [InlineData("123\tSmokehouse")]
    [InlineData("123 Smokehouse\0Ln")]
    public void Control_characters_are_rejected(string payload)
    {
        // AC-07 defense: control characters (log/label-pipeline injection
        // vectors) never survive validation.
        var fields = ValidFields(Market.US);
        fields[AddressFieldNames.StreetAddress] = payload;

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), Market.US));

        var error = Assert.Single(rejection.Errors);
        Assert.Equal(AddressRejectionCode.InvalidCharacters, error.Code);
    }

    [Fact]
    public void Oversized_values_are_rejected()
    {
        var fields = ValidFields(Market.US);
        fields[AddressFieldNames.StreetAddress] = new string('a', 201);

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), Market.US));

        var error = Assert.Single(rejection.Errors);
        Assert.Equal(AddressRejectionCode.ValueTooLong, error.Code);
    }

    [Fact]
    public void Market_without_configured_schema_fails_closed()
    {
        // Fail closed (issue 038, Failure Behavior): a market with no active
        // schema rejects everything instead of accepting unvalidated input.
        var usOnly = new AddressValidator(LaunchMarketAddressSchemas.All.Where(s => s.Market == Market.US));

        var rejection = Assert.Throws<AddressRejectedException>(
            () => usOnly.Validate(new AddressSubmission(ValidFields(Market.DE)), Market.DE));

        var error = Assert.Single(rejection.Errors);
        Assert.Equal(AddressRejectionCode.MarketNotConfigured, error.Code);
    }

    [Fact]
    public void Rejection_reports_all_invalid_fields_but_never_submitted_values()
    {
        // Failure behavior: fields identified generically; PII values never
        // echo into the exception (SECURITY.md, Logging rules 4-5).
        const string SecretishValue = "78701-EXFIL";
        var fields = ValidFields(Market.US);
        fields[AddressFieldNames.ZipCode] = SecretishValue;
        fields.Remove(AddressFieldNames.City);

        var rejection = Assert.Throws<AddressRejectedException>(
            () => Validator().Validate(new AddressSubmission(fields), Market.US));

        Assert.Equal(2, rejection.Errors.Count);
        Assert.DoesNotContain(SecretishValue, rejection.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Jane Doe", rejection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validated_address_ToString_redacts_pii()
    {
        var address = Validator().Validate(new AddressSubmission(ValidFields(Market.JP)), Market.JP);

        var formatted = address.ToString();

        Assert.DoesNotContain("渋谷", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("山田", formatted, StringComparison.Ordinal);
        Assert.Contains("redacted", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gating_rules_are_data_one_schema_per_market()
    {
        // CC-MKT-006 spirit for addresses: the rules are one declarative
        // schema per launch market, introspectable as data.
        Assert.Equal(Market.All.Count, LaunchMarketAddressSchemas.All.Count);
        Assert.Equal(
            Market.All.Select(m => m.Code).Order().ToArray(),
            LaunchMarketAddressSchemas.All.Select(s => s.Market.Code).Order().ToArray());
    }

    [Fact]
    public void Duplicate_schemas_for_one_market_are_rejected_at_construction()
    {
        var duplicated = LaunchMarketAddressSchemas.All.Concat(
            LaunchMarketAddressSchemas.All.Where(s => s.Market == Market.JP));

        Assert.Throws<ArgumentException>(() => new AddressValidator(duplicated));
    }
}
