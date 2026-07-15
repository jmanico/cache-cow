using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 018 (CC-SEC-001, CC-CNT-004; SECURITY.md, HTTP boundary rules 4, 6, 7):
/// CORS exact-origin allowlist, 405 on undeclared methods, 415 on unexpected
/// media types, 413 above the body cap, page-size clamping.
/// </summary>
public sealed class CorsMethodMediaSizeTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = TestHostBuilder.Create();

    public void Dispose() => _factory.Dispose();

    [Fact]
    [Requirement("CC-SEC-001")]
    public async Task Allowlisted_origin_receives_cors_grant()
    {
        using var client = _factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/__test/anonymous", UriKind.Relative));
        request.Headers.Add("Origin", "https://storefront.test");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://storefront.test", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public async Task Non_allowlisted_origin_receives_no_cors_grant()
    {
        using var client = _factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/__test/anonymous", UriKind.Relative));
        request.Headers.Add("Origin", "https://evil.test");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public async Task Preflight_from_allowlisted_origin_succeeds()
    {
        using var client = _factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/__test/anonymous", UriKind.Relative));
        request.Headers.Add("Origin", "https://storefront.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://storefront.test", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public void Non_https_cors_origin_prevents_host_startup()
    {
        using var factory = TestHostBuilder.Create(new Dictionary<string, string?>
        {
            ["Security:Cors:AllowedOrigins:0"] = "http://plaintext.example",
        });

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("exact HTTPS origin", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public async Task Undeclared_method_returns_405_problem_details()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(new Uri("/__test/get-only", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public async Task Unexpected_content_type_returns_415()
    {
        using var client = _factory.CreateHttpsClient();
        // Authenticated: for anonymous requests the deny-by-default fallback
        // (issue 020) denies the auto-generated 415 endpoint first, which is
        // fail-closed and acceptable; the 415 semantics are asserted on the
        // authenticated path.
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");

        using var content = new StringContent("plain body", Encoding.UTF8, "text/plain");
        var response = await client.PostAsync(new Uri("/__test/echo-json", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    [Requirement("CC-CNT-004")]
    public async Task Body_above_the_configured_cap_returns_413_and_never_reaches_the_handler()
    {
        using var client = _factory.CreateHttpsClient();

        // Test config caps MaxRequestBodyBytes at 2048.
        var oversized = new string('x', 4096);
        using var content = new StringContent($"{{\"message\":\"{oversized}\"}}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(new Uri("/__test/echo-json", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public async Task Body_within_the_cap_is_accepted()
    {
        using var client = _factory.CreateHttpsClient();

        using var content = new StringContent("{\"message\":\"small\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(new Uri("/__test/echo-json", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-001")]
    public async Task Page_size_above_the_maximum_is_clamped_server_side()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/page?pageSize=10000", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("100", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
