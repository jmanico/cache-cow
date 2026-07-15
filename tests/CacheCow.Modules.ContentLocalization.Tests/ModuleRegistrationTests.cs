using CacheCow.Modules.ContentLocalization.Email;
using CacheCow.Modules.ContentLocalization.Rendering;
using CacheCow.Modules.ContentLocalization.Resources;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// The module registers the resource pipeline, allowlist renderer, and email
/// composition with provisional port defaults (TryAdd) the host can replace
/// once the open decisions and later adapters (resource files/Contentful,
/// ACS dispatch) land.
/// </summary>
public sealed class ModuleRegistrationTests
{
    [Fact]
    [Requirement("CC-I18N-002")]
    [Requirement("CC-SEC-002")]
    [Requirement("CC-ORD-007")]
    public void AddContentLocalizationModule_resolves_every_service()
    {
        using var provider = new ServiceCollection()
            .AddContentLocalizationModule()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<StringResourceRegistry>());
        Assert.NotNull(provider.GetRequiredService<LocalizedMessageFormatter>());
        Assert.NotNull(provider.GetRequiredService<MarketPrimaryLocales>());
        Assert.NotNull(provider.GetRequiredService<AllowlistRichTextRenderer>());
        Assert.IsType<SchemeAllowlistUrlPolicy>(provider.GetRequiredService<IHyperlinkUrlPolicy>());
        Assert.IsType<InMemoryContentSource>(provider.GetRequiredService<IContentSource>());
        Assert.NotNull(provider.GetRequiredService<OrderEmailComposer>());
        Assert.NotNull(provider.GetRequiredService<OrderEmailService>());
        Assert.IsType<InMemoryEmailDispatch>(provider.GetRequiredService<IEmailDispatch>());
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Host_supplied_port_implementations_win_over_the_provisional_defaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmailDispatch, FakeDispatch>();
        services.AddContentLocalizationModule();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeDispatch>(provider.GetRequiredService<IEmailDispatch>());
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void The_default_resource_source_is_the_flagged_placeholder_copy_and_validates_clean()
    {
        // Real copy is a content task (issue 043; DESIGN.md §9); the
        // structurally-complete placeholder set must itself pass every
        // validation gate it will hold real copy to.
        var result = TranslationResourceValidator.Validate(PlaceholderOrderEmailResources.Set);

        Assert.True(result.IsValid, string.Join('\n', result.Violations.Select(v => v.Detail)));
    }

    private sealed class FakeDispatch : IEmailDispatch
    {
        public Task DispatchAsync(ComposedOrderEmail email, string recipientEmailAddress, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
