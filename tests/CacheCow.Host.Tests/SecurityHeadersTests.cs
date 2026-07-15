using System.Net;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 017 (CC-SEC-003; SECURITY.md, HTTP boundary rules 2-3): strict CSP on
/// HTML responses, every-response headers, no-store on authenticated and
/// sensitive responses, Report-Only rollout, payment-origin allowlisting.
/// </summary>
public sealed class SecurityHeadersTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = TestHostBuilder.Create();

    public void Dispose() => _factory.Dispose();

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Every_response_carries_nosniff_referrer_policy_and_permissions_policy()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/anonymous", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.NotEmpty(response.Headers.GetValues("Permissions-Policy"));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Error_responses_also_carry_security_headers()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/no-such-route", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Html_response_carries_strict_csp_with_response_nonce()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/html", UriKind.Relative), TestContext.Current.CancellationToken);
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy-Report-Only"));
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("script-src 'nonce-", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("base-uri 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("form-action 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("*", csp, StringComparison.Ordinal);

        // The SSR-rendered document's script carries the response's nonce.
        var nonce = ExtractNonce(csp);
        Assert.Contains($"nonce=\"{nonce}\"", body, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Csp_nonce_varies_per_response()
    {
        using var client = _factory.CreateHttpsClient();

        var first = await client.GetAsync(new Uri("/__test/html", UriKind.Relative), TestContext.Current.CancellationToken);
        var second = await client.GetAsync(new Uri("/__test/html", UriKind.Relative), TestContext.Current.CancellationToken);

        var firstNonce = ExtractNonce(Assert.Single(first.Headers.GetValues("Content-Security-Policy-Report-Only")));
        var secondNonce = ExtractNonce(Assert.Single(second.Headers.GetValues("Content-Security-Policy-Report-Only")));
        Assert.NotEqual(firstNonce, secondNonce);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Non_html_responses_do_not_carry_csp()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/anonymous", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Content-Security-Policy-Report-Only"));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Enforcement_mode_uses_the_enforced_header_name()
    {
        using var factory = TestHostBuilder.Create(new Dictionary<string, string?>
        {
            ["Security:Csp:ReportOnly"] = "false",
        });
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/html", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Content-Security-Policy-Report-Only"));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Configured_payment_processor_origins_appear_exactly_in_csp()
    {
        using var factory = TestHostBuilder.Create(new Dictionary<string, string?>
        {
            ["Security:Csp:PaymentProcessorOrigins:FormAction:0"] = "https://pay.processor.example",
            ["Security:Csp:PaymentProcessorOrigins:FrameSrc:0"] = "https://frames.processor.example",
            ["Security:Csp:PaymentProcessorOrigins:ConnectSrc:0"] = "https://api.processor.example",
        });
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/html", UriKind.Relative), TestContext.Current.CancellationToken);
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy-Report-Only"));

        Assert.Contains("form-action 'self' https://pay.processor.example", csp, StringComparison.Ordinal);
        Assert.Contains("frame-src https://frames.processor.example", csp, StringComparison.Ordinal);
        Assert.Contains("connect-src 'self' https://api.processor.example", csp, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public void Wildcard_payment_origin_prevents_host_startup()
    {
        using var factory = TestHostBuilder.Create(new Dictionary<string, string?>
        {
            ["Security:Csp:PaymentProcessorOrigins:FormAction:0"] = "https://*.processor.example",
        });

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("exact HTTPS origin", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    [Requirement("CC-SEC-013")]
    public async Task Sensitive_endpoint_response_is_no_store()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/sensitive", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    [Requirement("CC-SEC-013")]
    public async Task Authenticated_response_is_no_store()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");

        var response = await client.GetAsync(new Uri("/__test/default-authz", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Anonymous_public_response_is_not_forced_no_store()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/anonymous", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotEqual(true, response.Headers.CacheControl?.NoStore);
    }

    private static string ExtractNonce(string csp)
    {
        const string marker = "'nonce-";
        var start = csp.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = csp.IndexOf('\'', start);
        return csp[start..end];
    }
}
