using System.Text;

namespace CacheCow.Host.Security;

/// <summary>
/// Builds the strict Content-Security-Policy header value (SECURITY.md, HTTP
/// boundary rule 2; CC-SEC-003): nonce-based scripts, no unsafe-inline in any
/// directive, frame-ancestors 'none', base-uri 'self', form-action 'self' plus
/// exact payment-processor origins only. Fonts and assets are self-hosted
/// (SECURITY.md, Deployment rule 10), so no CDN origins appear anywhere.
/// </summary>
public static class ContentSecurityPolicy
{
    public const string EnforcedHeaderName = "Content-Security-Policy";
    public const string ReportOnlyHeaderName = "Content-Security-Policy-Report-Only";

    public static string HeaderName(CspSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.ReportOnly ? ReportOnlyHeaderName : EnforcedHeaderName;
    }

    public static string Build(string nonce, CspSettings settings)
    {
        ArgumentException.ThrowIfNullOrEmpty(nonce);
        ArgumentNullException.ThrowIfNull(settings);

        var origins = settings.PaymentProcessorOrigins;
        var policy = new StringBuilder()
            .Append("default-src 'self'; ")
            .Append("script-src 'nonce-").Append(nonce).Append("'; ")
            .Append("style-src 'self'; ")
            .Append("img-src 'self'; ")
            .Append("font-src 'self'; ")
            .Append("object-src 'none'; ")
            .Append("base-uri 'self'; ")
            .Append("frame-ancestors 'none'; ")
            .Append("form-action 'self'").AppendOrigins(origins.FormAction).Append("; ");

        if (origins.FrameSrc.Count > 0)
        {
            policy.Append("frame-src").AppendOrigins(origins.FrameSrc).Append("; ");
        }
        else
        {
            policy.Append("frame-src 'none'; ");
        }

        policy.Append("connect-src 'self'").AppendOrigins(origins.ConnectSrc);

        if (!string.IsNullOrEmpty(settings.ReportEndpoint))
        {
            policy.Append("; report-uri ").Append(settings.ReportEndpoint)
                  .Append("; report-to csp-endpoint");
        }

        return policy.ToString();
    }

    private static StringBuilder AppendOrigins(this StringBuilder builder, IEnumerable<string> origins)
    {
        foreach (var origin in origins)
        {
            builder.Append(' ').Append(origin);
        }

        return builder;
    }
}
