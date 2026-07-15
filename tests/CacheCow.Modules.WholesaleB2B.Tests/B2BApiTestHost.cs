using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CacheCow.Modules.WholesaleB2B.Api;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Gating;
using CacheCow.Modules.WholesaleB2B.Invoices;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.PriceLists;
using CacheCow.Modules.WholesaleB2B.Webhooks;
using CacheCow.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Minimal in-process host wiring ONLY the WholesaleB2B module (never
/// CacheCow.Host): a claims-injecting test authentication scheme stands in for
/// the host's JwtBearer (the module validates claim policy, not signatures),
/// and the cross-context ports are registered as fakes. Seeds two non-IN
/// partners plus one IN-market partner so cross-tenant and gating-parity
/// suites run against realistic tenancy (CC-QA-005, CC-API-007).
/// </summary>
internal sealed class B2BApiTestHost : IAsyncDisposable
{
    internal const string ClientA = "client-a"; // partner-a: DE
    internal const string ClientB = "client-b"; // partner-b: DE + JP
    internal const string ClientIn = "client-in"; // partner-in: IN
    internal const string ClientSuspended = "client-suspended"; // partner-x: suspended

    internal static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET"); // non-veg
    internal static readonly SkuId Ribs = SkuId.Parse("SKU-RIBS"); // non-veg
    internal static readonly SkuId Paneer = SkuId.Parse("SKU-PANEER"); // veg
    internal static readonly SkuId Jackfruit = SkuId.Parse("SKU-JACKFRUIT"); // veg

    private readonly WebApplication _app;

    private B2BApiTestHost(WebApplication app, FakeB2BGatingCheck gating, FakeWholesaleInvoiceReader invoices)
    {
        _app = app;
        Gating = gating;
        Invoices = invoices;
    }

    internal FakeB2BGatingCheck Gating { get; }

    internal FakeWholesaleInvoiceReader Invoices { get; }

    internal WebApplication App => _app;

    internal IServiceProvider Services => _app.Services;

    internal static async Task<B2BApiTestHost> StartAsync(Action<IServiceCollection>? configure = null)
    {
        var gating = new FakeB2BGatingCheck();
        var invoices = new FakeWholesaleInvoiceReader();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = B2BTestAuthHandler.Scheme;
            options.DefaultChallengeScheme = B2BTestAuthHandler.Scheme;
        }).AddScheme<AuthenticationSchemeOptions, B2BTestAuthHandler>(B2BTestAuthHandler.Scheme, null);
        builder.Services.AddAuthorization();

        builder.Services.AddSingleton<IPartnerAuditSink>(new RecordingPartnerAuditSink());
        builder.Services.AddSingleton<IB2BClientDirectory>(BuildDirectory());
        builder.Services.AddSingleton<IB2BGatingCheck>(gating);
        builder.Services.AddSingleton<IWholesaleInvoiceReader>(invoices);
        builder.Services.AddSingleton<IWebhookAddressResolver>(new FakeWebhookAddressResolver());
        builder.Services.AddWholesaleB2BModule();
        configure?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapWholesaleB2BApi();

        await app.StartAsync(TestContext.Current.CancellationToken);

        var host = new B2BApiTestHost(app, gating, invoices);
        host.SeedPriceLists();
        return host;
    }

    internal HttpClient CreateClient() => _app.GetTestClient();

    /// <summary>A request carrying a default-valid B2B token shape for <paramref name="clientId"/>.</summary>
    internal static HttpRequestMessage Request(
        HttpMethod method,
        string path,
        string clientId,
        string scopes,
        bool senderConstrained = true,
        long? issuedAtUnix = null,
        long? expiresAtUnix = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(B2BTestAuthHandler.ClientHeader, clientId);
        request.Headers.Add(B2BTestAuthHandler.ScopesHeader, scopes);
        if (senderConstrained)
        {
            request.Headers.Add(B2BTestAuthHandler.CnfHeader, """{"x5t#S256":"test-thumbprint"}""");
        }

        if (issuedAtUnix is { } iat)
        {
            request.Headers.Add(B2BTestAuthHandler.IatHeader, iat.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (expiresAtUnix is { } exp)
        {
            request.Headers.Add(B2BTestAuthHandler.ExpHeader, exp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return request;
    }

    private static FakeB2BClientDirectory BuildDirectory()
    {
        var workflow = new PartnerOnboardingWorkflow(new RecordingPartnerAuditSink());
        var directory = new FakeB2BClientDirectory();
        directory.Add(ClientA, Fixtures.TenantIn(PartnerOnboardingState.Approved, workflow, "partner-a", Fixtures.DeIdentity()));
        directory.Add(ClientB, Fixtures.TenantIn(
            PartnerOnboardingState.Approved, workflow, "partner-b", Fixtures.DeIdentity(), Fixtures.JpIdentity()));
        directory.Add(ClientIn, Fixtures.TenantIn(PartnerOnboardingState.Approved, workflow, "partner-in", Fixtures.InIdentity()));
        directory.Add(ClientSuspended, Fixtures.TenantIn(PartnerOnboardingState.Suspended, workflow, "partner-x", Fixtures.DeIdentity()));
        return directory;
    }

    private void SeedPriceLists()
    {
        var store = Services.GetRequiredService<InMemoryWholesalePriceLists>();

        store.Register(new WholesalePriceList(PartnerId.Parse("partner-a"), Market.DE,
        [
            new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(29_988, Currency.Eur)),
            new WholesalePriceLine(Paneer, 24, Money.FromMinorUnits(19_992, Currency.Eur)),
        ]));

        store.Register(new WholesalePriceList(PartnerId.Parse("partner-b"), Market.DE,
        [
            new WholesalePriceLine(Ribs, 12, Money.FromMinorUnits(24_988, Currency.Eur)),
        ]));

        // The IN list deliberately carries a non-veg row: the gating port —
        // not list composition — is the enforcement point under test
        // (CC-API-007 parity with CC-MKT-003).
        store.Register(new WholesalePriceList(PartnerId.Parse("partner-in"), Market.IN,
        [
            new WholesalePriceLine(Paneer, 24, Money.FromMinorUnits(99_900, Currency.Inr)),
            new WholesalePriceLine(Jackfruit, 24, Money.FromMinorUnits(89_900, Currency.Inr)),
            new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(199_900, Currency.Inr)),
        ]));
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>
/// Stands in for the host's JwtBearer pipeline: builds an authenticated
/// principal from test headers. Signature validation is host scope; the
/// module's claim policy is what these tests exercise (issue 054).
/// </summary>
internal sealed class B2BTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal new const string Scheme = "B2BTest";
    internal const string ClientHeader = "X-Test-Client";
    internal const string ScopesHeader = "X-Test-Scopes";
    internal const string CnfHeader = "X-Test-Cnf";
    internal const string IatHeader = "X-Test-Iat";
    internal const string ExpHeader = "X-Test-Exp";

    public B2BTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ClientHeader, out var clientId) || clientId.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new("client_id", clientId.ToString()),
            new("iat", Header(IatHeader) ?? now.AddSeconds(-30).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("exp", Header(ExpHeader) ?? now.AddMinutes(10).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };

        if (Header(ScopesHeader) is { } scopes)
        {
            claims.Add(new Claim("scp", scopes));
        }

        if (Header(CnfHeader) is { } cnf)
        {
            claims.Add(new Claim("cnf", cnf));
        }

        var identity = new ClaimsIdentity(claims, Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }

    private string? Header(string name) =>
        Request.Headers.TryGetValue(name, out var value) && value.Count > 0 ? value.ToString() : null;
}

/// <summary>Client-id → tenant mapping fake for <see cref="IB2BClientDirectory"/>.</summary>
internal sealed class FakeB2BClientDirectory : IB2BClientDirectory
{
    private readonly Dictionary<string, PartnerTenant> _byClientId = new(StringComparer.Ordinal);

    internal void Add(string clientId, PartnerTenant tenant) => _byClientId[clientId] = tenant;

    internal bool Throw { get; set; }

    public PartnerTenant? FindByClientId(string clientId)
    {
        if (Throw)
        {
            throw new InvalidOperationException("client directory unavailable (test fault injection)");
        }

        return _byClientId.TryGetValue(clientId, out var tenant) ? tenant : null;
    }
}

/// <summary>
/// Gating-port fake (host adapts the MarketGating service in production):
/// denies the known non-veg SKUs in the IN market, permits everything else,
/// and can be switched to throw for fail-closed probes (CC-API-007, AC-07).
/// </summary>
internal sealed class FakeB2BGatingCheck : IB2BGatingCheck
{
    private static readonly HashSet<SkuId> NonVeg = [B2BApiTestHost.Brisket, B2BApiTestHost.Ribs];

    internal bool Throw { get; set; }

    internal int Evaluations { get; private set; }

    public B2BGatingDecision EvaluateSku(Market market, SkuId sku)
    {
        Evaluations++;
        if (Throw)
        {
            throw new InvalidOperationException("gating service unavailable (test fault injection)");
        }

        return market == Market.IN && NonVeg.Contains(sku)
            ? B2BGatingDecision.Denied
            : B2BGatingDecision.Permitted;
    }
}

/// <summary>Tenant-scoped invoice-reader fake for <see cref="IWholesaleInvoiceReader"/>.</summary>
internal sealed class FakeWholesaleInvoiceReader : IWholesaleInvoiceReader
{
    private readonly Dictionary<string, (PartnerId Owner, WholesaleInvoiceSummary Invoice)> _invoices =
        new(StringComparer.Ordinal);

    internal void Add(PartnerId owner, WholesaleInvoiceSummary invoice) =>
        _invoices[invoice.InvoiceId] = (owner, invoice);

    public WholesaleInvoiceSummary? FindInvoice(PartnerTenantContext context, string invoiceId) =>
        _invoices.TryGetValue(invoiceId, out var entry) && entry.Owner == context.PartnerId
            ? entry.Invoice
            : null;
}

/// <summary>Deterministic resolver: every hostname resolves to a public address unless configured otherwise.</summary>
internal sealed class FakeWebhookAddressResolver : IWebhookAddressResolver
{
    internal Dictionary<string, IPAddress[]> ByHost { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal bool Throw { get; set; }

    public IReadOnlyList<IPAddress> Resolve(string hostName)
    {
        if (Throw)
        {
            throw new InvalidOperationException("DNS unavailable (test fault injection)");
        }

        return ByHost.TryGetValue(hostName, out var addresses)
            ? addresses
            : [IPAddress.Parse("93.184.216.34")];
    }
}
