using System.Net;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 021 (CC-API-006; SECURITY.md, Logging rules 1-2): RFC 9457
/// ProblemDetails for every error outside Development, with a correlation ID
/// and no stack traces, exception messages, SQL, file paths or internal
/// identifiers.
/// </summary>
public sealed class ProblemDetailsTests
{
    [Fact]
    [Requirement("CC-API-006")]
    public async Task Unhandled_exception_outside_development_returns_sanitized_problem_details()
    {
        using var factory = TestHostBuilder.Create(environment: "Production");
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/throws", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("correlationId", body, StringComparison.Ordinal);

        // No internal detail of any kind (issue 021 AC-01/AC-07).
        Assert.DoesNotContain("internal-diagnostic-detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.internal", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at CacheCow", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs", body, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task Unmatched_route_returns_404_problem_details()
    {
        using var factory = TestHostBuilder.Create(environment: "Production");
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/definitely-not-a-route", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("correlationId", body, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task Development_environment_may_render_the_developer_exception_page()
    {
        using var factory = TestHostBuilder.Create();
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/throws", UriKind.Relative), TestContext.Current.CancellationToken);

        // WebApplicationFactory runs in Development: the developer exception
        // page is permitted there and only there (issue 021 AC-02).
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
