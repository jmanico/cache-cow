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

var app = builder.Build();

// Pipeline intentionally empty: middleware (TLS/HSTS, CSP, authn/authz order —
// SECURITY.md, HTTP boundary rule 5) lands in issues 016–022; endpoints land in
// their bounded contexts' issues. The scaffold proves one-deployable packaging
// only (issue 001, AC-01/AC-06).
app.Run();

/// <summary>Exposed for WebApplicationFactory-based smoke tests.</summary>
public partial class Program
{
}
