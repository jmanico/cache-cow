using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CacheCow.Modules.ContentLocalization.Contact;
using CacheCow.Modules.ContentLocalization.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Minimal in-process host wiring ONLY this module (never CacheCow.Host), so
/// the contact endpoint is exercised across a real HTTP boundary. The host's
/// deny-by-default authorization fallback (issue 020; SECURITY.md,
/// Authentication rule 1) is reproduced here and probed by
/// <see cref="AuthenticatedProbePath"/>: that probe answering 401 is what makes
/// POST /contact answering 200 evidence of a deliberate
/// <c>AllowAnonymous</c> opt-out rather than an absent gate.
///
/// The rate limiter, body-size caps, security headers, and RFC 9457 exception
/// shaping are host middleware (issues 016-021) and are deliberately NOT wired:
/// this module owns the policy NAME it attaches, not the limiter.
/// </summary>
internal sealed class ContactTestHost : IAsyncDisposable
{
    internal const string Recipient = "contact-ops@cachecow.example";
    internal const long MinimumFillMilliseconds = 3_000;
    internal const string AuthenticatedProbePath = "/authenticated-probe";

    internal const string ValidName = "Ada Lovelace";
    internal const string ValidEmail = "ada@example.com";
    internal const string ValidTopic = "order";
    internal const string ValidMessage = "Where is my brisket order? It was due yesterday.";

    private static readonly JsonSerializerOptions ClientJson = new(JsonSerializerDefaults.Web);

    private readonly WebApplication _app;

    private ContactTestHost(WebApplication app, StubAbuseChallengeVerifier verifier)
    {
        _app = app;
        Verifier = verifier;
    }

    internal StubAbuseChallengeVerifier Verifier { get; }

    internal IServiceProvider Services => _app.Services;

    /// <summary>Every message the endpoint handed to the dispatch port; empty means nothing was processed.</summary>
    internal IReadOnlyList<DispatchedEmail> Dispatched =>
        Services.GetRequiredService<InMemoryEmailDispatch>().Dispatched;

    /// <param name="configured">
    /// False omits <see cref="ContactFormOptions"/>, reproducing a deployment
    /// where the open decisions (recipient, fill-time threshold) have not been
    /// made — the endpoint must fail closed rather than improvise a default.
    /// </param>
    internal static async Task<ContactTestHost> StartAsync(bool configured = true)
    {
        var verifier = new StubAbuseChallengeVerifier();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = UnauthenticatedTestAuthHandler.Scheme;
            options.DefaultChallengeScheme = UnauthenticatedTestAuthHandler.Scheme;
        }).AddScheme<AuthenticationSchemeOptions, UnauthenticatedTestAuthHandler>(
            UnauthenticatedTestAuthHandler.Scheme, null);

        builder.Services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        // Host-supplied ports win over the module's TryAdd defaults: the stub
        // verifier stands in for the ratified CAPTCHA-equivalent mechanism
        // (open decision, issue 076).
        builder.Services.AddSingleton<IAbuseChallengeVerifier>(verifier);
        if (configured)
        {
            builder.Services.AddSingleton(new ContactFormOptions(Recipient, MinimumFillMilliseconds));
        }

        builder.Services.AddContentLocalizationModule();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapContactEndpoints();
        app.MapGet(AuthenticatedProbePath, () => Results.Text("never reached"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new ContactTestHost(app, verifier);
    }

    internal HttpClient CreateClient() => _app.GetTestClient();

    /// <summary>A submission that passes every control unless a caller overrides a field.</summary>
    internal static ContactSubmissionRequest Valid(
        string? name = ValidName,
        string? email = ValidEmail,
        string? topic = ValidTopic,
        string? message = ValidMessage,
        string? website = null,
        long? fillTimeMs = MinimumFillMilliseconds + 1_000,
        string? challengeResponse = "challenge-token") =>
        new(name, email, topic, message, website, fillTimeMs, challengeResponse);

    /// <summary>
    /// Serializes through the real JSON encoder, so a CR/LF payload arrives at
    /// the server as genuine CR/LF bytes (escaped in transit) — the header
    /// injection corpus is delivered, not defanged by the test client.
    /// </summary>
    internal static HttpContent Payload(ContactSubmissionRequest request) =>
        Json(JsonSerializer.Serialize(request, ClientJson));

    internal static HttpContent Json(string body) => new StringContent(body, Encoding.UTF8, "application/json");

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>
/// Stands in for the host's authentication pipeline with a principal that is
/// never authenticated: the fallback policy therefore denies every endpoint
/// that has not explicitly opted out.
/// </summary>
internal sealed class UnauthenticatedTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal new const string Scheme = "ContactTest";

    public UnauthenticatedTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}

/// <summary>
/// Stands in for a ratified CAPTCHA-equivalent verifier (the mechanism is an
/// open decision, issue 076): records what it was shown and can be switched to
/// deny or to throw, so the endpoint's fail-closed handling is provable
/// (SECURITY.md, Logging rule 2).
/// </summary>
internal sealed class StubAbuseChallengeVerifier : IAbuseChallengeVerifier
{
    internal AbuseChallengeDecision Decision { get; set; } = AbuseChallengeDecision.Allowed;

    internal bool Throw { get; set; }

    internal List<AbuseChallengeEvidence> Seen { get; } = [];

    public ValueTask<AbuseChallengeDecision> VerifyAsync(AbuseChallengeEvidence evidence, CancellationToken cancellationToken)
    {
        Seen.Add(evidence);
        if (Throw)
        {
            throw new InvalidOperationException("challenge verifier unavailable (test fault injection)");
        }

        return ValueTask.FromResult(Decision);
    }
}
