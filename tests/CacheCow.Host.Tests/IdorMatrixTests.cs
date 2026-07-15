using System.Net;
using System.Text.Json;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// One protected resource route in the cross-tenant/cross-role IDOR matrix
/// (CC-QA-005). SecretMarker is content that MUST NOT appear in any
/// out-of-scope response; ExpectedBody is what the rightful caller receives.
/// RequiredRole/WrongRole apply only to role-gated routes.
/// </summary>
public sealed record ProtectedResourceRoute(
    string Name,
    HttpMethod Method,
    string Path,
    string NonexistentPath,
    string ExpectedBody,
    string SecretMarker,
    string OwnerUser,
    string? OwnerTenant,
    string ForeignUser,
    string? ForeignTenant,
    string? RequiredRole = null,
    string? WrongRole = null);

/// <summary>
/// Issue 062 (CC-SEC-007, CC-QA-005; SECURITY.md, Authentication rules 8-9;
/// Deployment rule 8): the reusable cross-tenant/cross-role ATTACK harness.
/// Every sensitive endpoint must be covered by this matrix as it lands -
/// module test suites subclass this and supply their routes, and the suite
/// runs on every merge. For each route it attempts access as: unauthenticated
/// (401), wrong tenant (404), wrong role (404), correct tenant and role (200),
/// asserting that no response ever confirms the existence of - or leaks the
/// content of - another tenant's resource, and that the out-of-scope 404 is
/// indistinguishable from a genuinely nonexistent resource.
/// NOTE (issue 062 AC-08, open question): the mechanical inventory that
/// derives "every sensitive endpoint" from route metadata still needs the
/// "sensitive" classification rule ratified by a human; until then coverage
/// is by module suites subclassing this harness.
/// </summary>
public abstract class IdorMatrixTestBase : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = TestHostBuilder.Create();

    /// <summary>The protected resource routes under attack.</summary>
    protected abstract IReadOnlyList<ProtectedResourceRoute> Routes { get; }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _factory.Dispose();
        }
    }

    [Fact]
    [Requirement("CC-QA-005")]
    [Requirement("CC-SEC-007")]
    public async Task Unauthenticated_access_is_challenged()
    {
        Assert.NotEmpty(Routes);
        foreach (var route in Routes)
        {
            using var client = CreateClient(user: null, tenant: null, role: null);
            using var response = await Send(client, route.Method, route.Path);
            Assert.True(
                response.StatusCode == HttpStatusCode.Unauthorized,
                $"{route.Name}: unauthenticated expected 401, got {(int)response.StatusCode}");
        }
    }

    [Fact]
    [Requirement("CC-QA-005")]
    [Requirement("CC-SEC-007")]
    public async Task Wrong_tenant_access_returns_404_with_no_existence_disclosure()
    {
        Assert.NotEmpty(Routes);
        foreach (var route in Routes)
        {
            // Only tenancy is wrong: the attacker holds any required role.
            using var client = CreateClient(route.ForeignUser, route.ForeignTenant, route.RequiredRole);
            using var response = await Send(client, route.Method, route.Path);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound,
                $"{route.Name}: wrong tenant expected 404 (not 403, not 200), got {(int)response.StatusCode}");
            AssertNoDisclosure(route, body);
        }
    }

    [Fact]
    [Requirement("CC-QA-005")]
    [Requirement("CC-SEC-007")]
    public async Task Wrong_role_access_returns_404_with_no_existence_disclosure()
    {
        foreach (var route in Routes.Where(r => r.RequiredRole is not null))
        {
            // Right tenant, insufficient role (CC-DSH-002 matrix posture).
            using var client = CreateClient(route.OwnerUser, route.OwnerTenant, route.WrongRole);
            using var response = await Send(client, route.Method, route.Path);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound,
                $"{route.Name}: wrong role expected 404 (not 403), got {(int)response.StatusCode}");
            AssertNoDisclosure(route, body);
        }
    }

    [Fact]
    [Requirement("CC-QA-005")]
    [Requirement("CC-SEC-007")]
    public async Task Correct_tenant_and_role_access_succeeds()
    {
        Assert.NotEmpty(Routes);
        foreach (var route in Routes)
        {
            using var client = CreateClient(route.OwnerUser, route.OwnerTenant, route.RequiredRole);
            using var response = await Send(client, route.Method, route.Path);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{route.Name}: rightful owner expected 200, got {(int)response.StatusCode}");
            Assert.Contains(route.ExpectedBody, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Requirement("CC-QA-005")]
    [Requirement("CC-SEC-007")]
    public async Task Out_of_scope_404_is_indistinguishable_from_nonexistent_404()
    {
        Assert.NotEmpty(Routes);
        foreach (var route in Routes)
        {
            using var foreign = CreateClient(route.ForeignUser, route.ForeignTenant, route.RequiredRole);
            using var deniedResponse = await Send(foreign, route.Method, route.Path);
            using var missingResponse = await Send(foreign, route.Method, route.NonexistentPath);
            var denied = await deniedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var missing = await missingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, deniedResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
            Assert.Equal(
                deniedResponse.Content.Headers.ContentType?.ToString(),
                missingResponse.Content.Headers.ContentType?.ToString());

            // Identical RFC 9457 shape apart from the per-request correlation
            // id (SECURITY.md, Authentication rule 9; Logging rule 1).
            Assert.Equal(ProblemShape(denied), ProblemShape(missing));
        }
    }

    private static string ProblemShape(string problemJson)
    {
        using var document = JsonDocument.Parse(problemJson);
        var properties = document.RootElement.EnumerateObject()
            .Where(p => p.Name is not "correlationId" and not "traceId")
            .Select(p => $"{p.Name}={p.Value.GetRawText()}");
        return string.Join(";", properties);
    }

    private static void AssertNoDisclosure(ProtectedResourceRoute route, string body)
    {
        Assert.DoesNotContain(route.SecretMarker, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(route.ExpectedBody, body, StringComparison.OrdinalIgnoreCase);
        if (route.OwnerTenant is not null)
        {
            Assert.DoesNotContain(route.OwnerTenant, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private HttpClient CreateClient(string? user, string? tenant, string? role)
    {
        var client = _factory.CreateHttpsClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        }

        if (tenant is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenant);
        }

        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        }

        return client;
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

/// <summary>
/// Issue 062: the mandatory IDOR classes (orders, invoices, addresses,
/// partner data - CC-QA-005) plus a role-gated dashboard-style resource, run
/// through the harness against the test-only sample endpoints, which enforce
/// through the same product ResourceAuthorization path module endpoints use.
/// </summary>
public sealed class SampleResourceIdorTests : IdorMatrixTestBase
{
    protected override IReadOnlyList<ProtectedResourceRoute> Routes { get; } =
    [
        new(
            "b2b order (CC-API-004)",
            HttpMethod.Get,
            "/__test/resources/orders/ord-a-1",
            "/__test/resources/orders/ord-does-not-exist",
            ExpectedBody: "order-secret-tenant-a",
            SecretMarker: "order-secret-tenant-a",
            OwnerUser: "alice",
            OwnerTenant: "tenant-a",
            ForeignUser: "mallory",
            ForeignTenant: "tenant-b"),
        new(
            "invoice (CC-API-004)",
            HttpMethod.Get,
            "/__test/resources/invoices/inv-a-1",
            "/__test/resources/invoices/inv-does-not-exist",
            ExpectedBody: "invoice-secret-tenant-a",
            SecretMarker: "invoice-secret-tenant-a",
            OwnerUser: "alice",
            OwnerTenant: "tenant-a",
            ForeignUser: "mallory",
            ForeignTenant: "tenant-b"),
        new(
            "consumer address (consumer-identity ownership)",
            HttpMethod.Get,
            "/__test/resources/addresses/addr-alice-1",
            "/__test/resources/addresses/addr-does-not-exist",
            ExpectedBody: "address-secret-alice",
            SecretMarker: "address-secret-alice",
            OwnerUser: "alice",
            OwnerTenant: null,
            ForeignUser: "mallory",
            ForeignTenant: "tenant-b"),
        new(
            "partner order creation (cross-tenant mutation refused)",
            HttpMethod.Post,
            "/__test/resources/partners/partner-a/orders",
            "/__test/resources/partners/partner-does-not-exist/orders",
            ExpectedBody: "partner-order-accepted",
            SecretMarker: "partner-terms-tenant-a",
            OwnerUser: "alice",
            OwnerTenant: "tenant-a",
            ForeignUser: "mallory",
            ForeignTenant: "tenant-b"),
        new(
            "role-gated ops note (CC-DSH-002 posture)",
            HttpMethod.Get,
            "/__test/resources/ops-notes/note-a-1",
            "/__test/resources/ops-notes/note-does-not-exist",
            ExpectedBody: "ops-note-secret-tenant-a",
            SecretMarker: "ops-note-secret-tenant-a",
            OwnerUser: "alice",
            OwnerTenant: "tenant-a",
            ForeignUser: "mallory",
            ForeignTenant: "tenant-b",
            RequiredRole: "ops-agent",
            WrongRole: "sales-viewer"),
    ];
}
