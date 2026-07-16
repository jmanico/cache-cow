using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CacheCow.Modules.BackOffice.Api;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Inventory;
using CacheCow.Modules.BackOffice.Orders;
using CacheCow.Modules.BackOffice.Partners;
using CacheCow.Modules.BackOffice.Rbac;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Minimal in-process host wiring ONLY the BackOffice module (never
/// CacheCow.Host): a claims-injecting test authentication scheme stands in for
/// the host's staff SSO — the module consumes an already-authenticated
/// principal and validates no token, so signatures are out of its scope — and
/// every cross-context port is a fake.
///
/// Seeds one order and one submitted partner so the gating, IDOR, and workflow
/// suites run against realistic shapes (CC-QA-005).
/// </summary>
internal sealed class DashboardTestHost : IAsyncDisposable
{
    private readonly WebApplication app;

    private DashboardTestHost(
        WebApplication app,
        RecordingAuditSink audit,
        FakeOrderReader orders,
        FakeOrderCommands orderCommands,
        FakeInventoryReader inventory,
        FakePartnerDirectory partners,
        FakePartnerWorkflow partnerWorkflow)
    {
        this.app = app;
        Audit = audit;
        Orders = orders;
        OrderCommands = orderCommands;
        Inventory = inventory;
        Partners = partners;
        PartnerWorkflow = partnerWorkflow;
    }

    internal RecordingAuditSink Audit { get; }

    internal FakeOrderReader Orders { get; }

    internal FakeOrderCommands OrderCommands { get; }

    internal FakeInventoryReader Inventory { get; }

    internal FakePartnerDirectory Partners { get; }

    internal FakePartnerWorkflow PartnerWorkflow { get; }

    internal static async Task<DashboardTestHost> StartAsync(Action<IServiceCollection>? configure = null)
    {
        var audit = new RecordingAuditSink();
        var orders = new FakeOrderReader();
        var orderCommands = new FakeOrderCommands();
        var inventory = new FakeInventoryReader();
        var partners = new FakePartnerDirectory();
        var partnerWorkflow = new FakePartnerWorkflow();

        orders.Add(DashboardTestData.Order());
        inventory.Add(DashboardTestData.Inventory());
        partners.Add(DashboardTestData.Partner());

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = StaffTestAuthHandler.Scheme;
            options.DefaultChallengeScheme = StaffTestAuthHandler.Scheme;
        }).AddScheme<AuthenticationSchemeOptions, StaffTestAuthHandler>(StaffTestAuthHandler.Scheme, null);
        builder.Services.AddAuthorization();

        // The host-supplied configuration the module deliberately ships no
        // default for: the TEST matrix (production content needs human
        // authoring) and a step-up max age (unratified — see StepUpPolicy).
        builder.Services.AddSingleton<IRolePermissionMatrixProvider>(
            new ConfiguredRolePermissionMatrixProvider(DashboardTestMatrix.Create()));
        builder.Services.AddSingleton<IStepUpPolicyProvider>(
            new ConfiguredStepUpPolicyProvider(StepUpPolicy.Create(TimeSpan.FromMinutes(5))));
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(DashboardTestData.Now));

        // The cross-context port adapters the host owns in production.
        builder.Services.AddSingleton<Auditing.IAuditEventSink>(audit);
        builder.Services.AddSingleton<IDashboardOrderReader>(orders);
        builder.Services.AddSingleton<IDashboardOrderCommands>(orderCommands);
        builder.Services.AddSingleton<IDashboardInventoryReader>(inventory);
        builder.Services.AddSingleton<IDashboardPartnerDirectory>(partners);
        builder.Services.AddSingleton<IDashboardPartnerWorkflow>(partnerWorkflow);

        builder.Services.AddBackOfficeModule();
        configure?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBackOfficeDashboard();

        await app.StartAsync(TestContext.Current.CancellationToken);

        return new DashboardTestHost(app, audit, orders, orderCommands, inventory, partners, partnerWorkflow);
    }

    internal HttpClient CreateClient() => app.GetTestClient();

    /// <summary>
    /// A request carrying an authenticated staff principal for
    /// <paramref name="role"/>.
    /// </summary>
    /// <param name="stepUpMinutesAgo">
    /// How long ago the step-up ceremony happened; null means none ever
    /// happened (the <c>auth_time</c> claim is absent).
    /// </param>
    internal static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        string role,
        string actorId = "staff-1",
        int? stepUpMinutesAgo = 1,
        int roleClaimCount = 1)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(StaffTestAuthHandler.ActorHeader, actorId);

        for (var i = 0; i < roleClaimCount; i++)
        {
            request.Headers.Add(StaffTestAuthHandler.RoleHeader, role);
        }

        if (stepUpMinutesAgo is { } minutes)
        {
            request.Headers.Add(
                StaffTestAuthHandler.AuthTimeHeader,
                DashboardTestData.Now.AddMinutes(-minutes).ToUnixTimeSeconds()
                    .ToString(CultureInfo.InvariantCulture));
        }

        return request;
    }

    /// <summary>An unauthenticated request (no staff principal at all).</summary>
    internal static HttpRequestMessage AnonymousRequest(HttpMethod method, string path) => new(method, path);

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}

/// <summary>
/// Stands in for the host's staff SSO pipeline (Entra ID with mandatory
/// passkeys, CC-DSH-001): builds an authenticated principal from test headers.
/// Authentication is host scope; what these tests exercise is the module's
/// claim reading and its server-side permission enforcement.
/// </summary>
internal sealed class StaffTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal new const string Scheme = "StaffTest";
    internal const string ActorHeader = "X-Test-Actor";
    internal const string RoleHeader = "X-Test-Role";
    internal const string AuthTimeHeader = "X-Test-Auth-Time";

    public StaffTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ActorHeader, out var actor) || actor.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(DashboardClaimTypes.Subject, actor.ToString()) };

        if (Request.Headers.TryGetValue(RoleHeader, out var roles))
        {
            foreach (var role in roles)
            {
                if (role is not null)
                {
                    claims.Add(new Claim(DashboardClaimTypes.Role, role));
                }
            }
        }

        if (Request.Headers.TryGetValue(AuthTimeHeader, out var authTime) && authTime.Count > 0)
        {
            claims.Add(new Claim(DashboardClaimTypes.AuthenticationTime, authTime.ToString()));
        }

        var identity = new ClaimsIdentity(claims, Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
