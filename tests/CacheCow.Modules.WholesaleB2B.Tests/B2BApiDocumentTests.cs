using System.Text.Json;
using CacheCow.Modules.WholesaleB2B.Api;
using CacheCow.Modules.WholesaleB2B.Api.Contracts;
using CacheCow.Modules.WholesaleB2B.Api.Schema;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 053 (CC-API-010; ARCHITECTURE.md, Dependency rule 7): schemas and the
/// API document generate from the SAME contract records and descriptors the
/// endpoints enforce — the mapped surface and the documented surface are
/// asserted identical, the generator is deterministic, and the schemas mirror
/// the strict validation semantics (additionalProperties: false, required
/// members, strict types). Doc HOSTING is an open decision (issue 053) — only
/// generation is in scope.
/// </summary>
public sealed class B2BApiDocumentTests
{
    [Fact]
    [Requirement("CC-API-010")]
    public void The_document_is_deterministic()
    {
        Assert.Equal(B2BApiDocumentGenerator.Generate(), B2BApiDocumentGenerator.Generate());
    }

    [Fact]
    [Requirement("CC-API-010")]
    [Requirement("CC-API-001")]
    public void The_document_publishes_the_version_and_deprecation_policy()
    {
        using var document = JsonDocument.Parse(B2BApiDocumentGenerator.Generate());
        var root = document.RootElement;

        Assert.Equal("v1", root.GetProperty("version").GetString());

        var policy = root.GetProperty("versioningPolicy");
        Assert.True(policy.GetProperty("minimumDeprecationWindowDays").GetInt32() >= 180);
        Assert.Contains("deprecation window", policy.GetProperty("breakingChanges").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-API-010")]
    public async Task The_mapped_endpoints_and_the_documented_endpoints_are_the_same_surface()
    {
        await using var host = await B2BApiTestHost.StartAsync();

        var mapped = ((IEndpointRouteBuilder)host.App).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => (
                Method: endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single(),
                Pattern: "/" + endpoint.RoutePattern.RawText!.TrimStart('/'),
                Descriptor: endpoint.Metadata.GetMetadata<B2BEndpointDescriptor>()))
            .ToArray();

        // Every mapped endpoint carries its descriptor as metadata, and the
        // descriptor is the exact object the document generates from.
        Assert.All(mapped, endpoint => Assert.NotNull(endpoint.Descriptor));
        Assert.All(mapped, endpoint => Assert.Equal(endpoint.Pattern, endpoint.Descriptor!.Pattern));
        Assert.All(mapped, endpoint => Assert.Equal(endpoint.Method, endpoint.Descriptor!.Method));
        Assert.All(mapped, endpoint => Assert.Contains(endpoint.Descriptor, B2BApiSurface.Endpoints));

        var mappedSurface = mapped.Select(e => (e.Method, e.Pattern)).Order().ToArray();
        var publishedSurface = B2BApiSurface.Endpoints.Select(d => (d.Method, d.Pattern)).Order().ToArray();
        Assert.Equal(publishedSurface, mappedSurface);

        // And the generated document lists exactly that surface.
        using var document = JsonDocument.Parse(B2BApiDocumentGenerator.Generate());
        var documented = document.RootElement.GetProperty("endpoints").EnumerateArray()
            .Select(e => (Method: e.GetProperty("method").GetString()!, Pattern: e.GetProperty("path").GetString()!))
            .Order()
            .ToArray();
        Assert.Equal(publishedSurface, documented);
    }

    [Fact]
    [Requirement("CC-API-010")]
    [Requirement("CC-API-006")]
    public void The_request_schema_mirrors_the_strict_validation_semantics()
    {
        var schema = B2BJsonSchemaGenerator.SchemaFor(typeof(CreateWholesaleOrderRequest));

        Assert.Equal("CreateWholesaleOrderRequest", schema["title"]!.GetValue<string>());
        Assert.Equal("object", schema["type"]!.GetValue<string>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());

        var required = schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        Assert.Equal(["lines", "market"], required);

        var lines = schema["properties"]!["lines"]!;
        Assert.Equal("array", lines["type"]!.GetValue<string>());

        var lineSchema = lines["items"]!;
        Assert.False(lineSchema["additionalProperties"]!.GetValue<bool>());
        Assert.Equal("integer", lineSchema["properties"]!["cases"]!["type"]!.GetValue<string>());
        Assert.Equal("string", lineSchema["properties"]!["sku"]!["type"]!.GetValue<string>());
    }

    [Fact]
    [Requirement("CC-API-010")]
    public void Response_schemas_generate_from_the_same_records_the_endpoints_serialize()
    {
        var schema = B2BJsonSchemaGenerator.SchemaFor(typeof(WholesaleOrderResponse));

        var properties = schema["properties"]!.AsObject().Select(p => p.Key).ToArray();
        Assert.Equal(
            ["createdAt", "currency", "lines", "market", "orderId", "status", "totalMinorUnits"],
            properties);
        Assert.Equal("integer", schema["properties"]!["totalMinorUnits"]!["type"]!.GetValue<string>());
        Assert.Equal("date-time", schema["properties"]!["createdAt"]!["format"]!.GetValue<string>());
    }

    [Fact]
    [Requirement("CC-API-010")]
    public void The_document_publishes_the_webhook_signing_contract()
    {
        using var document = JsonDocument.Parse(B2BApiDocumentGenerator.Generate());
        var webhooks = document.RootElement.GetProperty("webhooks");

        Assert.Equal("CacheCow-Webhook-Signature", webhooks.GetProperty("signatureHeader").GetString());
        Assert.Equal("CacheCow-Webhook-Timestamp", webhooks.GetProperty("timestampHeader").GetString());
        Assert.Equal(300, webhooks.GetProperty("replayToleranceSeconds").GetInt64());
    }
}
