using System.Globalization;
using CacheCow.Host.Security;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.TestSupport;

/// <summary>
/// TEST-ONLY sample endpoints. These are NOT product endpoints and carry no
/// CC-* feature scope (REQUIREMENTS.md §17: unreferenced product paths are
/// scope creep - these exist solely so the integration-test suite can observe
/// the security middleware of issues 016-022 in-process). They are mapped
/// only when the "CacheCow:TestSurface" configuration flag is true, which the
/// shipped configuration never sets; only the test project's
/// WebApplicationFactory enables it. Several endpoints resolve services (test
/// counter, test-only authorization policy) that exist only in test
/// composition, so the surface is non-functional outside the test host even
/// if the flag were set.
/// </summary>
public static class TestOnlyEndpoints
{
    public const string ConfigurationFlag = "CacheCow:TestSurface";

    /// <summary>Test-only order counter proving no state change occurs on rejected requests.</summary>
    public sealed class OrderCounter
    {
        private int _count;

        public int Value => Volatile.Read(ref _count);

        public int Increment() => Interlocked.Increment(ref _count);
    }

    public sealed record EchoPayload(string? Message);

    public static void MapIfEnabled(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Configuration.GetValue<bool>(ConfigurationFlag))
        {
            return;
        }

        var group = app.MapGroup("/__test");

        // Deny-by-default probes (issue 020): no authorization metadata at all
        // -> fallback policy applies.
        group.MapGet("/protected", () => Results.Text("protected-ok"));
        group.MapGet("/default-authz", () => Results.Text("default-authz-ok")).RequireAuthorization();
        group.MapGet("/anonymous", () => Results.Text("anonymous-ok")).AllowAnonymous();

        // Fail-closed authorization probe (issue 021 AC-03): the
        // "test-throwing" policy and its throwing handler are registered by
        // the test project only.
        group.MapGet("/throwing-authz", () => Results.Text("never-served")).RequireAuthorization("test-throwing");

        // Header probes (issues 016/017).
        group.MapGet("/html", (HttpContext context) => Results.Content(
                $"<!doctype html><html><head><title>test</title></head><body>" +
                $"<script nonce=\"{CspNonce.Get(context)}\">/* nonce probe */</script></body></html>",
                "text/html"))
            .AllowAnonymous();
        group.MapGet("/sensitive", () => Results.Text("sensitive-ok"))
            .AllowAnonymous()
            .WithMetadata(new SensitiveResponseAttribute());

        // Method/media/size probes (issue 018).
        group.MapGet("/get-only", () => Results.Text("get-ok")).AllowAnonymous();
        group.MapPost("/echo-json", (EchoPayload payload) => Results.Json(payload)).AllowAnonymous();
        group.MapGet("/page", (int? pageSize, IOptions<SecurityOptions> options) =>
                Results.Text(PageSizeLimiter.Clamp(pageSize, options.Value.RequestLimits)
                    .ToString(CultureInfo.InvariantCulture)))
            .AllowAnonymous();

        // Rate-limit probes (issue 019).
        group.MapGet("/limited-auth", () => Results.Text("limited-auth-ok"))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Authentication);
        group.MapPost("/limited-order", (OrderCounter counter) =>
                Results.Text(counter.Increment().ToString(CultureInfo.InvariantCulture)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.OrderCreation);

        // Error-shape probe (issue 021): the exception message simulates
        // internal detail that must never reach a client.
        group.MapGet("/throws", string () =>
                throw new InvalidOperationException(
                    "internal-diagnostic-detail: Server=sql.internal;Database=cachecow"))
            .AllowAnonymous();
    }
}
