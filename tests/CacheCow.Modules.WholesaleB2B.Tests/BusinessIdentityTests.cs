using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 049, AC-03 (CC-WHS-002): per-market business-identity schema
/// validation — DE requires a USt-IdNr., IN requires a GSTIN; invalid values
/// are rejected server-side, never coerced (SECURITY.md, Input validation
/// rule 1). US/ES/MX/JP formats are an open decision (issue 049, Open
/// Questions), so those markets require only a bounded non-empty identifier.
/// </summary>
public sealed class BusinessIdentityTests
{
    [Fact]
    [Requirement("CC-WHS-002")]
    public void DE_accepts_a_structurally_valid_UstIdNr()
    {
        var identity = BusinessIdentity.Create(Market.DE, "DE123456789");

        Assert.Equal(Market.DE, identity.Market);
        Assert.Equal("DE123456789", identity.Value);
    }

    [Theory]
    [Requirement("CC-WHS-002")]
    [InlineData("DE12345678")]      // 8 digits
    [InlineData("DE1234567890")]    // 10 digits
    [InlineData("123456789")]       // no country prefix
    [InlineData("de123456789")]     // lowercase prefix: reject, never coerce
    [InlineData("DE12345678A")]     // letter where a digit is required
    [InlineData("DE 123456789")]    // embedded whitespace
    [InlineData("ATU12345678")]     // another member state's VAT format
    public void DE_rejects_a_malformed_UstIdNr(string value)
    {
        Assert.Throws<InvalidBusinessIdentityException>(() => BusinessIdentity.Create(Market.DE, value));
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void IN_accepts_a_structurally_valid_GSTIN()
    {
        var identity = BusinessIdentity.Create(Market.IN, "27AAPFU0939F1ZV");

        Assert.Equal(Market.IN, identity.Market);
        Assert.Equal("27AAPFU0939F1ZV", identity.Value);
    }

    [Theory]
    [Requirement("CC-WHS-002")]
    [InlineData("27AAPFU0939F1Z")]    // 14 chars
    [InlineData("27AAPFU0939F1ZVX")]  // 16 chars
    [InlineData("27aapfu0939f1zv")]   // lowercase: reject, never coerce
    [InlineData("2XAAPFU0939F1ZV")]   // state code must be two digits
    [InlineData("27AAP4U0939F1ZV")]   // digit inside the PAN letter block
    [InlineData("27AAPFUO939F1ZV")]   // letter inside the PAN digit block
    [InlineData("27AAPFU09391F1Z")]   // 12th char must be a letter
    [InlineData("27AAPFU0939F0ZV")]   // entity code '0' is invalid
    [InlineData("27AAPFU0939F1AV")]   // 14th char must be the literal 'Z'
    [InlineData("27AAPFU0939F1Z!")]   // check character must be alphanumeric
    public void IN_rejects_a_malformed_GSTIN(string value)
    {
        Assert.Throws<InvalidBusinessIdentityException>(() => BusinessIdentity.Create(Market.IN, value));
    }

    [Theory]
    [Requirement("CC-WHS-002")]
    [InlineData("US", "12-3456789")]        // EIN-shaped, accepted as opaque
    [InlineData("ES", "B12345678")]
    [InlineData("MX", "ABC680524P76")]
    [InlineData("JP", "T1234567890123")]
    public void Unenumerated_markets_accept_a_non_empty_identifier(string marketCode, string value)
    {
        // The required fields for these markets are an open decision
        // (issue 049, Open Questions) — only presence is enforced here.
        var market = Market.Parse(marketCode);
        var identity = BusinessIdentity.Create(market, value);

        Assert.Equal(market, identity.Market);
        Assert.Equal(value, identity.Value);
    }

    [Theory]
    [Requirement("CC-WHS-002")]
    [InlineData("US", "")]
    [InlineData("US", "   ")]
    [InlineData("JP", " T1234567890123")] // surrounding whitespace: reject, never trim into acceptance
    public void Unenumerated_markets_still_reject_empty_or_padded_identifiers(string marketCode, string value)
    {
        Assert.Throws<InvalidBusinessIdentityException>(
            () => BusinessIdentity.Create(Market.Parse(marketCode), value));
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Overlong_opaque_identifiers_are_rejected()
    {
        var overlong = new string('A', 129);

        Assert.Throws<InvalidBusinessIdentityException>(() => BusinessIdentity.Create(Market.US, overlong));
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void An_uninitialized_market_is_rejected()
    {
        Assert.Throws<WholesaleValidationException>(() => BusinessIdentity.Create(default, "DE123456789"));
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Validation_failures_and_ToString_never_echo_the_confidential_identifier()
    {
        // Tax registration numbers are Confidential (issue 049, Data
        // Classification); they must not leak through error messages or
        // accidental logging (SECURITY.md, Logging rules 1 and 4).
        const string Submitted = "DE99SECRET9";
        var exception = Assert.Throws<InvalidBusinessIdentityException>(
            () => BusinessIdentity.Create(Market.DE, Submitted));
        Assert.DoesNotContain(Submitted, exception.Message, StringComparison.Ordinal);

        var identity = BusinessIdentity.Create(Market.DE, "DE123456789");
        Assert.DoesNotContain(identity.Value, identity.ToString(), StringComparison.Ordinal);
    }
}
