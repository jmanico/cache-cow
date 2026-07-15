using CacheCow.Host.Security;
using CacheCow.Host.TestSupport;
using CacheCow.Modules.BackOffice;
using CacheCow.Modules.CatalogInventory;
using CacheCow.Modules.ContentLocalization;
using CacheCow.Modules.Fulfillment;
using CacheCow.Modules.IdentityAccess;
using CacheCow.Modules.Invoicing;
using CacheCow.Modules.MarketGating;
using CacheCow.Modules.OrderingPayments;
using CacheCow.Modules.PricingPromotions;
using CacheCow.Modules.WholesaleB2B;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMarketGatingModule()
    .AddCatalogInventoryModule()
    .AddPricingPromotionsModule()
    .AddOrderingPaymentsModule()
    .AddFulfillmentModule()
    .AddWholesaleB2BModule()
    .AddInvoicingModule()
    .AddBackOfficeModule()
    .AddIdentityAccessModule()
    .AddContentLocalizationModule();

// Issues 016-022: transport, headers, CORS/limits, rate limiting,
// deny-by-default authorization, RFC 9457 errors, security-event logging.
builder.Services.AddCacheCowSecurity();

// Kestrel transport-level body cap (SECURITY.md, HTTP boundary rule 7); the
// in-pipeline RequestBodySizeLimitMiddleware applies the same configured cap
// per request. API-host plaintext rejection (SECURITY.md, HTTP boundary
// rule 1: no HTTP listener at all, never a redirect) is Kestrel endpoint /
// ingress *configuration*, not middleware: the deployment binds only TLS
// endpoints for API hosts (issue 016 AC-02/AC-06; the TLS termination
// topology is an engineering decision flagged in issue 016's open questions).
// It is not exercisable in-process because the test server bypasses listener
// binding; the in-process suite covers redirect + HSTS + ordering instead.
builder.WebHost.ConfigureKestrel((context, kestrel) =>
{
    var limits = context.Configuration
        .GetSection(SecurityOptions.SectionName)
        .Get<SecurityOptions>()?.RequestLimits ?? new RequestLimitSettings();
    kestrel.Limits.MaxRequestBodySize = limits.MaxRequestBodyBytes;
});

var app = builder.Build();

// Middleware in the order fixed by SECURITY.md HTTP boundary rule 5
// (issue 016): HTTPS/HSTS -> security headers -> static files ->
// authentication -> authorization, with errors/status codes outermost.
app.UseCacheCowSecurityPipeline();

// Test-only sample endpoints; never enabled by shipped configuration.
TestOnlyEndpoints.MapIfEnabled(app);

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program
{
}
