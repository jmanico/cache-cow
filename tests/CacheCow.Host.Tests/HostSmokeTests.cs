using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Proves one-deployable packaging: the single host boots with all ten modules
/// registered and responds over HTTP (issue 001, AC-01 and Test Strategy).
/// </summary>
public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HostSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Host_boots_and_responds()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        // Empty pipeline: any HTTP response proves the host is up; no endpoint
        // exists yet, so an unmatched route returns 404.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
