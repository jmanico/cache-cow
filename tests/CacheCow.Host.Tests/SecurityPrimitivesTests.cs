using CacheCow.Host.Security;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Unit coverage for the security configuration primitives: exact-origin
/// validation (issues 017/018 AC-02), the CSP builder, the options validator,
/// and the page-size clamp.
/// </summary>
public sealed class SecurityPrimitivesTests
{
    [Theory]
    [Requirement("CC-SEC-003")]
    [InlineData("https://checkout.processor.example")]
    [InlineData("https://checkout.processor.example:8443")]
    public void Exact_https_origins_are_accepted(string origin) =>
        Assert.True(ExactOrigin.IsValid(origin));

    [Theory]
    [Requirement("CC-SEC-003")]
    [InlineData("https://*.processor.example")]
    [InlineData("*")]
    [InlineData("http://checkout.processor.example")]
    [InlineData("https://checkout.processor.example/path")]
    [InlineData("https://checkout.processor.example/")]
    [InlineData("https://checkout.processor.example?q=1")]
    [InlineData("https://user@checkout.processor.example")]
    [InlineData("checkout.processor.example")]
    [InlineData("")]
    [InlineData(null)]
    public void Wildcards_suffix_patterns_plaintext_and_non_origins_are_rejected(string? origin) =>
        Assert.False(ExactOrigin.IsValid(origin));

    [Fact]
    [Requirement("CC-SEC-003")]
    public void Csp_builder_emits_strict_directives_without_unsafe_inline()
    {
        var csp = ContentSecurityPolicy.Build("abc123", new CspSettings());

        Assert.Contains("script-src 'nonce-abc123'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("base-uri 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("form-action 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-src 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public void Csp_builder_appends_report_directives_when_configured()
    {
        var settings = new CspSettings { ReportEndpoint = "/csp-reports" };

        var csp = ContentSecurityPolicy.Build("abc123", settings);

        Assert.Contains("report-uri /csp-reports", csp, StringComparison.Ordinal);
        Assert.Contains("report-to csp-endpoint", csp, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public void Report_only_setting_selects_the_header_name()
    {
        Assert.Equal(
            ContentSecurityPolicy.ReportOnlyHeaderName,
            ContentSecurityPolicy.HeaderName(new CspSettings { ReportOnly = true }));
        Assert.Equal(
            ContentSecurityPolicy.EnforcedHeaderName,
            ContentSecurityPolicy.HeaderName(new CspSettings { ReportOnly = false }));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public void Validator_accepts_the_shipped_defaults()
    {
        var result = new SecurityOptionsValidator().Validate(null, new SecurityOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public void Validator_rejects_wildcard_payment_origins()
    {
        var options = new SecurityOptions();
        options.Csp.PaymentProcessorOrigins.FormAction.Add("https://*.processor.example");

        var result = new SecurityOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public void Validator_rejects_credentials_without_an_origin_allowlist()
    {
        var options = new SecurityOptions();
        options.Cors.AllowCredentials = true;

        var result = new SecurityOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    [Requirement("CC-API-008")]
    public void Validator_rejects_non_positive_rate_limits()
    {
        var options = new SecurityOptions();
        options.RateLimiting.Default.PermitLimit = 0;

        var result = new SecurityOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [Requirement("CC-SEC-001")]
    [InlineData(null, 20)]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Page_sizes_clamp_to_the_configured_bounds(int? requested, int expected) =>
        Assert.Equal(expected, PageSizeLimiter.Clamp(requested, new RequestLimitSettings()));
}
