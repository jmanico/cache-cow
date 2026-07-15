using System.Net;
using CacheCow.Host.Security;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 016 (CC-SEC-003, CC-SEC-008; SECURITY.md, HTTP boundary rules 1 and 5):
/// HTTPS redirection, HSTS with preload, and pipeline ordering - security
/// headers before static files, authentication before authorization.
/// API-host plaintext-listener rejection is Kestrel endpoint/ingress
/// configuration and cannot be exercised in-process (see Program.cs note).
/// </summary>
public sealed class TransportAndPipelineTests : IDisposable
{
    private readonly string _staticRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public TransportAndPipelineTests()
    {
        _staticRoot = Directory.CreateTempSubdirectory("cachecow-static").FullName;
        File.WriteAllText(Path.Combine(_staticRoot, "probe.css"), "body{}");
        _factory = TestHostBuilder.Create(staticRoot: _staticRoot);
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_staticRoot, recursive: true);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    [Requirement("CC-SEC-008")]
    public async Task Plaintext_request_is_redirected_to_https_with_no_content()
    {
        using var client = _factory.CreateHttpClient();

        var response = await client.GetAsync(new Uri("/__test/anonymous", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location?.Scheme);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Https_response_carries_hsts_with_preload()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/anonymous", UriKind.Relative), TestContext.Current.CancellationToken);

        var hsts = Assert.Single(response.Headers.GetValues("Strict-Transport-Security"));
        Assert.Contains("max-age=31536000", hsts, StringComparison.Ordinal);
        Assert.Contains("includeSubDomains", hsts, StringComparison.Ordinal);
        Assert.Contains("preload", hsts, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Static_file_response_carries_security_headers()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/probe.css", UriKind.Relative), TestContext.Current.CancellationToken);

        // Header middleware ran before static files (SECURITY.md, HTTP
        // boundary rule 5): even asset responses carry the headers.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.NotEmpty(response.Headers.GetValues("Strict-Transport-Security"));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public void Canonical_pipeline_order_satisfies_http_boundary_rule_5()
    {
        var order = SecurityPipeline.CanonicalOrder;
        int Index(string stage) => order.IndexOf(stage, StringComparer.Ordinal);

        Assert.True(Index("HttpsRedirection") < Index("SecurityHeaders"));
        Assert.True(Index("Hsts") < Index("StaticFiles"));
        Assert.True(Index("SecurityHeaders") < Index("StaticFiles"));
        Assert.True(Index("StaticFiles") < Index("Authentication"));
        Assert.True(Index("Authentication") < Index("Authorization"));
    }

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task Authorization_evaluates_the_authenticated_principal()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");

        var response = await client.GetAsync(new Uri("/__test/protected", UriKind.Relative), TestContext.Current.CancellationToken);

        // 200 is only possible if authentication middleware populated the
        // principal before authorization evaluated the fallback policy.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> list, string value, StringComparer comparer)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (comparer.Equals(list[i], value))
            {
                return i;
            }
        }

        return -1;
    }
}
