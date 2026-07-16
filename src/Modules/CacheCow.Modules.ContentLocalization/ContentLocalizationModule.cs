using CacheCow.Modules.ContentLocalization.Contact;
using CacheCow.Modules.ContentLocalization.Email;
using CacheCow.Modules.ContentLocalization.Rendering;
using CacheCow.Modules.ContentLocalization.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.ContentLocalization;

/// <summary>
/// Registration entry point for the Content &amp; Localization bounded
/// context (ARCHITECTURE.md, "Server bounded contexts" 10): ICU MessageFormat
/// string resources (issue 064, CC-I18N-002), the sanitizing allowlist
/// renderer for CMS content (issue 072, CC-SEC-002/CC-CNT-001), and
/// transactional order emails (issue 043, CC-ORD-007/CC-I18N-006).
/// </summary>
public static class ContentLocalizationModule
{
    public static IServiceCollection AddContentLocalizationModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // --- Issue 064: string-resource pipeline (CC-I18N-002). Ports carry
        // provisional defaults (TryAdd, host-replaceable): the resource-file /
        // Contentful-backed source is a later adapter; the default is the
        // structurally-complete placeholder email copy (flagged: real copy is
        // a content task, DESIGN.md §9).
        services.TryAddSingleton<IStringResourceSource>(
            new InMemoryStringResourceSource(PlaceholderOrderEmailResources.Set));
        services.TryAddSingleton(static provider =>
            StringResourceRegistry.Create(provider.GetRequiredService<IStringResourceSource>().Load()));

        // Market-primary fallback locales (CC-I18N-006). The default covers
        // only the five unambiguous markets; IN (en-IN vs hi-IN) is an open
        // decision and MUST be supplied by the host via
        // MarketPrimaryLocales.WithIndiaPrimary once decided.
        services.TryAddSingleton(MarketPrimaryLocales.Default);
        services.TryAddSingleton<LocalizedMessageFormatter>();

        // --- Issue 072: sanitizing allowlist renderer (CC-SEC-002, CC-CNT-001,
        // CC-SEC-004). The Contentful delivery adapter and publish-event
        // receiver are later issues; the content-source port defaults to empty.
        services.TryAddSingleton<IHyperlinkUrlPolicy, SchemeAllowlistUrlPolicy>();
        services.TryAddSingleton<IRegisteredDomainAllowlist>(
            new InMemoryRegisteredDomainAllowlist([])); // empty allowlist: fail closed until configured
        services.TryAddSingleton<AllowlistRichTextRenderer>();
        services.TryAddSingleton<IContentSource, InMemoryContentSource>();

        // --- Issue 043: transactional order emails (CC-ORD-007, CC-I18N-006).
        // The Azure Communication Services dispatch adapter is a later issue.
        services.TryAddSingleton<OrderEmailComposer>();
        services.TryAddSingleton<InMemoryEmailDispatch>();
        services.TryAddSingleton<IEmailDispatch>(static provider =>
            provider.GetRequiredService<InMemoryEmailDispatch>());
        services.TryAddSingleton<OrderEmailService>();

        // --- Issue 076: contact form (CC-CNT-004; SECURITY.md, Input
        // validation rule 10). The CAPTCHA-equivalent mechanism is an open
        // decision, so the default verifier denies every submission (fail
        // closed, host-replaceable). ContactFormOptions is deliberately NOT
        // registered: the internal recipient and fill-time threshold are host
        // configuration; without them the endpoint answers 503. The HOST maps
        // the route via ContactEndpoints.MapContactEndpoints and registers
        // the "contact-form" rate-limiter policy (issue 019).
        services.TryAddSingleton<IAbuseChallengeVerifier, UnconfiguredAbuseChallengeVerifier>();

        return services;
    }
}
