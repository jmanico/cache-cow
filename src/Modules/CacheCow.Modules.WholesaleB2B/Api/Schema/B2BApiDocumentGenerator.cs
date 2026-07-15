using System.Text.Json;
using System.Text.Json.Nodes;
using CacheCow.Modules.WholesaleB2B.Webhooks;

namespace CacheCow.Modules.WholesaleB2B.Api.Schema;

/// <summary>
/// Generates the deterministic B2B API document from the SAME sources the
/// running service enforces: the <see cref="B2BApiSurface"/> descriptors that
/// are attached to the mapped endpoints, and schemas reflected from the
/// contract records by <see cref="B2BJsonSchemaGenerator"/> (CC-API-010;
/// ARCHITECTURE.md, Dependency rule 7 — no hand-maintained parallel
/// definitions). Byte-identical across runs, so CI can diff the artifact.
///
/// This is a doc-GENERATION service only: where and how the rendered
/// documentation is hosted (and where the CC-API-001 deprecation policy is
/// published to partners beyond this artifact) is an open decision — flagged,
/// not guessed (issue 053, Open Questions; presentation rules live in
/// DESIGN.md §11 and are out of scope here).
/// </summary>
public static class B2BApiDocumentGenerator
{
    private static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };

    public static string Generate()
    {
        var endpoints = new JsonArray();
        foreach (var descriptor in B2BApiSurface.Endpoints
                     .OrderBy(d => d.Pattern, StringComparer.Ordinal)
                     .ThenBy(d => d.Method, StringComparer.Ordinal))
        {
            var endpoint = new JsonObject
            {
                ["method"] = descriptor.Method,
                ["path"] = descriptor.Pattern,
                ["requiredScope"] = descriptor.RequiredScope,
                ["rateLimitPolicy"] = descriptor.RateLimitPolicy,
                ["idempotencyKeyHeaderRequired"] = descriptor.RequiresIdempotencyKey,
                ["requirements"] = new JsonArray(
                    [.. descriptor.RequirementIds.Order(StringComparer.Ordinal).Select(id => (JsonNode)id)]),
            };

            if (descriptor.RequestContract is not null)
            {
                endpoint["requestSchema"] = B2BJsonSchemaGenerator.SchemaFor(descriptor.RequestContract);
            }

            endpoint["responseSchema"] = B2BJsonSchemaGenerator.SchemaFor(descriptor.ResponseContract);
            endpoints.Add((JsonNode)endpoint);
        }

        var document = new JsonObject
        {
            ["api"] = "cache-cow-wholesale-b2b",
            ["version"] = B2BApiSurface.Version,
            ["versioningPolicy"] = new JsonObject
            {
                ["breakingChanges"] =
                    "Breaking changes increment the API version (e.g. /v2); the previous version remains "
                    + "supported for the documented deprecation window (CC-API-001).",
                ["minimumDeprecationWindowDays"] = B2BApiSurface.MinimumDeprecationWindowDays,
            },
            ["errors"] = new JsonObject
            {
                ["format"] = "RFC 9457 problem details (application/problem+json); generic bodies only (CC-API-006)",
            },
            ["webhooks"] = new JsonObject
            {
                ["signatureHeader"] = WebhookSigner.SignatureHeader,
                ["timestampHeader"] = WebhookSigner.TimestampHeader,
                ["keyIdHeader"] = WebhookSigner.KeyIdHeader,
                ["signature"] = "HMAC-SHA256 over \"{timestamp}.{body}\" with the partner's current secret, "
                    + "sent as \"" + WebhookSigner.SignatureScheme + "=<lowercase hex>\" (CC-API-009)",
                ["replayToleranceSeconds"] = (long)WebhookSigner.DefaultReplayTolerance.TotalSeconds,
            },
            ["endpoints"] = endpoints,
        };

        return document.ToJsonString(OutputOptions);
    }
}
