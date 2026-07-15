using CacheCow.Modules.ContentLocalization.Rendering;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 072 / CC-SEC-004: data-derived URLs validate against the
/// https/mailto/tel scheme allowlist with '#' fallback; partner and locator
/// links additionally validate against a registered-domain allowlist supplied
/// through a configuration port (SECURITY.md, Input validation rule 6).
/// </summary>
public sealed class HyperlinkUrlPolicyTests
{
    [Theory]
    [Requirement("CC-SEC-004")]
    [InlineData("https://cachecow.example/menu", "https://cachecow.example/menu")]
    [InlineData("mailto:pit@cachecow.example", "mailto:pit@cachecow.example")]
    [InlineData("tel:+498912345", "tel:+498912345")]
    [InlineData("HTTPS://UPPER.example/PATH", "https://upper.example/PATH")]
    [InlineData("javascript:alert(document.cookie)", "#")]
    [InlineData("data:text/html,<script>alert(1)</script>", "#")]
    [InlineData("ftp://example.com/file", "#")]
    [InlineData("http://example.com/", "#")]
    [InlineData(" ", "#")]
    [InlineData(null, "#")]
    public void Scheme_allowlist_passes_https_mailto_tel_and_falls_back_to_hash(string? destination, string expected)
    {
        Assert.Equal(expected, new SchemeAllowlistUrlPolicy().Resolve(destination));
    }

    [Theory]
    [Requirement("CC-SEC-004")]
    [InlineData("https://partner.example/store", "https://partner.example/store")]  // exact registered domain
    [InlineData("https://shop.partner.example/x", "https://shop.partner.example/x")] // subdomain, label boundary
    [InlineData("https://evilpartner.example/x", "#")]                               // suffix without label boundary
    [InlineData("https://other.example/x", "#")]                                     // not allowlisted
    [InlineData("http://partner.example/x", "#")]                                    // https only
    [InlineData("mailto:a@partner.example", "#")]                                    // mailto is not a partner link
    [InlineData("javascript:alert(1)", "#")]
    public void Partner_links_require_https_and_an_allowlisted_registered_domain(string destination, string expected)
    {
        var policy = new PartnerLinkUrlPolicy(new InMemoryRegisteredDomainAllowlist(["partner.example"]));

        Assert.Equal(expected, policy.Resolve(destination));
    }

    [Fact]
    [Requirement("CC-SEC-004")]
    public void An_empty_domain_allowlist_fails_closed()
    {
        var policy = new PartnerLinkUrlPolicy(new InMemoryRegisteredDomainAllowlist([]));

        Assert.Equal("#", policy.Resolve("https://anything.example/"));
    }
}
