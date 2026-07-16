using System.Net;
using System.Net.Http.Json;
using System.Text;
using CacheCow.Modules.BackOffice.Api;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issues 082/084/085 at the HTTP boundary, against an in-module TestServer
/// (never CacheCow.Host). Asserts the responses the acceptance criteria name:
/// 404-not-403 for every authorization denial, the step-up refusal on refunds,
/// no-store on every response, RFC 9457 problems with no internal detail, and
/// strict binding.
///
/// The role grants come from <see cref="DashboardTestMatrix"/> — a TEST MATRIX
/// ONLY. The production role–permission matrix is unauthored and needs a human
/// decision (issue 080, Open Questions; each of 082/084/085 records the same
/// gap). Nothing here asserts which role SHOULD hold a permission.
/// </summary>
public sealed class DashboardEndpointTests
{
    // ---- deny-by-default and staff identity -------------------------------

    [Fact]
    [Requirement("CC-DSH-001")]
    public async Task UnauthenticatedRequest_IsRejectedOnEveryEndpoint()
    {
        // Issue 084 AC-05: no endpoint is reachable without authentication.
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        foreach (var descriptor in DashboardApiSurface.Endpoints)
        {
            var path = Concrete(descriptor.Pattern);
            using var response = await client.SendAsync(
                DashboardTestHost.AnonymousRequest(new HttpMethod(descriptor.Method), path),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public async Task AmbiguousRoleClaims_FailClosed()
    {
        // Whether roles are combinable per staff member is unresolved (issue
        // 080, Open Questions); two role claims must not be resolved by
        // guessing one.
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, "/dashboard/orders", "ops-agent", roleClaimCount: 2),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- the cross-role matrix (CC-QA-005) -------------------------------

    /// <summary>
    /// Every endpoint × every role: exactly the roles the TEST matrix grants
    /// succeed, and every other role gets 404 — never 403, which would confirm
    /// the endpoint exists and the role is merely under-privileged
    /// (SECURITY.md, Authentication rule 9; issue 082 AC-06, 084 AC-04, 085
    /// AC-06).
    /// </summary>
    [Theory]
    // orders.search -> ops-agent
    [InlineData("GET", "/dashboard/orders", "ops-agent", HttpStatusCode.OK)]
    [InlineData("GET", "/dashboard/orders", "finance", HttpStatusCode.NotFound)]
    [InlineData("GET", "/dashboard/orders", "sales-viewer", HttpStatusCode.NotFound)]
    [InlineData("GET", "/dashboard/orders", "hr-admin", HttpStatusCode.NotFound)]
    [InlineData("GET", "/dashboard/orders", "admin", HttpStatusCode.NotFound)]
    // orders.refund -> finance
    [InlineData("POST", "/dashboard/orders/CC-ORD-1001/refund", "finance", HttpStatusCode.OK)]
    [InlineData("POST", "/dashboard/orders/CC-ORD-1001/refund", "ops-agent", HttpStatusCode.NotFound)]
    [InlineData("POST", "/dashboard/orders/CC-ORD-1001/refund", "admin", HttpStatusCode.NotFound)]
    // inventory.view -> ops-agent
    [InlineData("GET", "/dashboard/inventory", "ops-agent", HttpStatusCode.OK)]
    [InlineData("GET", "/dashboard/inventory", "finance", HttpStatusCode.NotFound)]
    [InlineData("GET", "/dashboard/inventory", "sales-viewer", HttpStatusCode.NotFound)]
    // partners.manage / partners.approve -> admin
    [InlineData("GET", "/dashboard/partners", "admin", HttpStatusCode.OK)]
    [InlineData("GET", "/dashboard/partners", "ops-agent", HttpStatusCode.NotFound)]
    [InlineData("GET", "/dashboard/partners/partner-a", "admin", HttpStatusCode.OK)]
    [InlineData("GET", "/dashboard/partners/partner-a", "finance", HttpStatusCode.NotFound)]
    [InlineData("POST", "/dashboard/partners/partner-a/approve", "admin", HttpStatusCode.OK)]
    [InlineData("POST", "/dashboard/partners/partner-a/approve", "ops-agent", HttpStatusCode.NotFound)]
    [InlineData("POST", "/dashboard/partners/partner-a/reject", "sales-viewer", HttpStatusCode.NotFound)]
    [InlineData("POST", "/dashboard/partners/partner-a/suspend", "hr-admin", HttpStatusCode.NotFound)]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public async Task RoleGating_GrantsOnlyWhatTheMatrixGrants_AndDeniesWith404(
        string method, string path, string role, HttpStatusCode expected)
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(new HttpMethod(method), path, role),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public async Task UnauthorizedRequest_NeverReachesTheCrossContextPorts()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/refund", "ops-agent"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, host.OrderCommands.Invocations);
        Assert.Empty(host.Audit.Appended);
    }

    // ---- 404-not-403 and IDOR (SECURITY.md, Authentication rule 9) --------

    [Fact]
    [Requirement("CC-DSH-002")]
    public async Task InaccessibleAndNonexistentOrders_AreIndistinguishable()
    {
        // The denial 404 and the unknown-resource 404 must look identical;
        // otherwise the pair is an existence oracle.
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var denied = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/refund", "admin"),
            TestContext.Current.CancellationToken);
        using var missing = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Post, "/dashboard/orders/CC-ORD-NOPE/refund", "finance"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(
            await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // ---- step-up on refunds (issue 082, AC-04) ---------------------------

    [Fact]
    [Requirement("CC-DSH-001")]
    [Requirement("CC-QA-005")]
    public async Task Refund_WithoutStepUp_IsRefusedAndNeverMovesMoney()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(
                HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/refund", "finance", stepUpMinutesAgo: null),
            TestContext.Current.CancellationToken);

        // 403 (not 404): the actor DOES hold the permission and must
        // re-authenticate — a 404 would be an unrecoverable dead end. It
        // leaks nothing, since the permission check precedes any lookup.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(host.OrderCommands.Refunds);
    }

    [Fact]
    [Requirement("CC-DSH-001")]
    public async Task Refund_WithStaleStepUp_IsRefused()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        // 6 hours ago against a 5-minute max age: session still valid, step-up
        // stale.
        using var response = await client.SendAsync(
            DashboardTestHost.Request(
                HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/refund", "finance", stepUpMinutesAgo: 360),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(host.OrderCommands.Refunds);
    }

    [Fact]
    [Requirement("CC-DSH-001")]
    [Requirement("CC-PRC-003")]
    public async Task Refund_WithFreshStepUp_ReturnsTheCanonicalAmountInMinorUnits()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/refund", "finance"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DashboardOrderRefundResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal("refunded", body!.State);
        Assert.Equal(29_988, body.RefundedMinorUnits);
        Assert.Equal("EUR", body.Currency);
    }

    // ---- transitions (issue 082, AC-02/AC-03) ----------------------------

    [Fact]
    [Requirement("CC-ORD-006")]
    [Requirement("CC-DSH-004")]
    public async Task Transition_WhenGranted_AppliesAndAudits()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = DashboardTestHost.Request(
            HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/transition", "ops-agent");
        request.Content = JsonContent.Create(new { targetState = "delivered" });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(host.OrderCommands.Transitions);
        Assert.Equal("orders.transition", host.Audit.Appended[0].Action);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public async Task Transition_ToAnUnknownStateName_IsRejectedBeforeAnyPortCall()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = DashboardTestHost.Request(
            HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/transition", "ops-agent");
        request.Content = JsonContent.Create(new { targetState = "teleported" });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.OrderCommands.Transitions);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public async Task Transition_TheStateMachineRefuses_IsAConflict()
    {
        // AC-03: an illegal transition submitted DIRECTLY against the endpoint,
        // bypassing the UI, is refused server-side with no state change.
        await using var host = await DashboardTestHost.StartAsync();
        host.OrderCommands.TransitionOutcome = Orders.DashboardOrderCommandOutcome.Rejected;
        using var client = host.CreateClient();

        using var request = DashboardTestHost.Request(
            HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/transition", "ops-agent");
        request.Content = JsonContent.Create(new { targetState = "packed" });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- strict binding (SECURITY.md, Input validation rules 1-3) ---------

    [Fact]
    [Requirement("CC-DSH-003")]
    public async Task Transition_WithUnknownBodyMembers_IsRejectedNotStripped()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = DashboardTestHost.Request(
            HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/transition", "ops-agent");

        // An attempt to smuggle server-controlled fields: unknown members are
        // rejected, never quietly dropped into acceptance.
        request.Content = JsonContent.Create(new { targetState = "delivered", actor = "someone-else", refundAmount = 1 });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.OrderCommands.Transitions);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    public async Task Transition_WithNonJsonContentType_Is415()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = DashboardTestHost.Request(
            HttpMethod.Post, "/dashboard/orders/CC-ORD-1001/transition", "ops-agent");
        request.Content = new StringContent("targetState=delivered", Encoding.UTF8, "application/x-www-form-urlencoded");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("/dashboard/orders?market=XX")]
    [InlineData("/dashboard/orders?state=teleported")]
    [InlineData("/dashboard/orders?page=0")]
    [InlineData("/dashboard/orders?page=notanumber")]
    [InlineData("/dashboard/orders?placedFrom=yesterday")]
    [InlineData("/dashboard/orders?page=1&page=2")]
    [Requirement("CC-DSH-003")]
    public async Task Search_WithMalformedQueryParameters_Is400(string path)
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, path, "ops-agent"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Orders.SearchCalls);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    public async Task Search_ClampsOversizedPageSizes_RatherThanRejectingThem()
    {
        // SECURITY.md, HTTP boundary rule 7: clamp page sizes.
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, "/dashboard/orders?pageSize=100000", "ops-agent"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DashboardPaging.MaxPageSize, host.Orders.LastQuery!.PageSize);
    }

    // ---- response hygiene (issue 084 AC-06, 085 AC-08) -------------------

    [Fact]
    [Requirement("CC-SEC-003")]
    public async Task EveryResponse_CarriesNoStore_IncludingErrors()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        // A success...
        using var ok = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, "/dashboard/inventory", "ops-agent"),
            TestContext.Current.CancellationToken);
        Assert.Equal("no-store", ok.Headers.CacheControl!.ToString());

        // ...and a denial: an authenticated, staff-personalized response is
        // never cacheable either way (SECURITY.md, HTTP boundary rules 3, 10).
        using var denied = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, "/dashboard/inventory", "finance"),
            TestContext.Current.CancellationToken);
        Assert.Equal("no-store", denied.Headers.CacheControl!.ToString());
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task ErrorResponses_AreRfc9457ProblemsWithNoInternalDetail()
    {
        await using var host = await DashboardTestHost.StartAsync();
        host.Orders.Throw = true;
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, "/dashboard/orders", "ops-agent"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // No exception message, stack trace, type name, or fault text leaks
        // (SECURITY.md, Logging rule 1).
        Assert.DoesNotContain("test fault injection", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CacheCow.Modules", body, StringComparison.Ordinal);
    }

    // ---- PII-minimal wire shape (issue 082, AC-01) -----------------------

    [Fact]
    [Requirement("CC-DSH-003")]
    public async Task OrderSearchResponse_CarriesNoCustomerPii()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, "/dashboard/orders", "ops-agent"),
            TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        foreach (var fragment in new[] { "customer", "email", "address", "phone" })
        {
            Assert.DoesNotContain(fragment, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Requirement("CC-DSH-006")]
    public async Task InventoryResponse_ReportsServiceLevelAsIntegerBasisPoints()
    {
        await using var host = await DashboardTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            DashboardTestHost.Request(HttpMethod.Get, "/dashboard/inventory", "ops-agent"),
            TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // 99.50% travels as 9950 — an exact integer, never a JSON float.
        Assert.Contains("\"serviceLevelBasisPoints\":9950", body, StringComparison.Ordinal);
        Assert.DoesNotContain("99.5", body, StringComparison.Ordinal);
    }

    // ---- the declared surface ---------------------------------------------

    [Fact]
    [Requirement("CC-DSH-003")]
    public void EveryDescriptor_DeclaresAStricterPolicyForStateChangingRoutes()
    {
        // SECURITY.md, HTTP boundary rule 7: stricter limits on sensitive
        // endpoints.
        foreach (var descriptor in DashboardApiSurface.Endpoints)
        {
            var expected = string.Equals(descriptor.Method, "POST", StringComparison.Ordinal)
                ? DashboardRateLimitPolicies.StaffCommands
                : DashboardRateLimitPolicies.Staff;

            Assert.Equal(expected, descriptor.RateLimitPolicy);
        }
    }

    [Fact]
    [Requirement("CC-SEC-011")]
    public void EveryRoute_LivesUnderTheDashboardBasePath()
    {
        // The dashboard origin is a separate, VPN-restricted origin; a route
        // escaping the prefix would escape that isolation (SECURITY.md, HTTP
        // boundary rule 8).
        Assert.All(DashboardApiSurface.Endpoints, descriptor =>
            Assert.StartsWith(DashboardApiSurface.BasePath + "/", descriptor.Pattern, StringComparison.Ordinal));
    }

    private static string Concrete(string pattern) => pattern
        .Replace("{orderRef}", "CC-ORD-1001", StringComparison.Ordinal)
        .Replace("{partnerId}", "partner-a", StringComparison.Ordinal);
}
