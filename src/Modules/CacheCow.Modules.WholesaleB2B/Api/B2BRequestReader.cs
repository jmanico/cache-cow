using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace CacheCow.Modules.WholesaleB2B.Api;

/// <summary>
/// Strict request-body deserialization at the trust boundary (SECURITY.md,
/// Input validation rules 1–3): bodies bind only to the dedicated contract
/// records, under options that make the runtime behavior match the published
/// schemas exactly (CC-API-006) — unknown members rejected
/// (<see cref="JsonUnmappedMemberHandling.Disallow"/>, never stripped into
/// acceptance), numbers strictly typed (a quoted number is a type error),
/// no comments, no trailing commas. Any violation is a 400 RFC 9457 problem
/// with a generic body: no parser internals, paths, or offsets leak
/// (SECURITY.md, Logging rule 1).
/// </summary>
internal static class B2BRequestReader
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    internal static async Task<(T? Request, IResult? Rejection)> ReadAsync<T>(HttpContext http)
        where T : class
    {
        if (!HasJsonContentType(http))
        {
            return (null, B2BProblems.UnsupportedMediaType());
        }

        try
        {
            var request = await http.Request.ReadFromJsonAsync<T>(Strict, http.RequestAborted);
            return request is null
                ? (null, B2BProblems.SchemaViolation())
                : (request, null);
        }
        catch (JsonException)
        {
            return (null, B2BProblems.SchemaViolation());
        }
        catch (InvalidDataException)
        {
            return (null, B2BProblems.SchemaViolation());
        }
    }

    /// <summary>Exactly application/json (with optional parameters) — anything else is 415 (SECURITY.md, HTTP boundary rule 6).</summary>
    private static bool HasJsonContentType(HttpContext http)
    {
        var contentType = http.Request.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }
}
