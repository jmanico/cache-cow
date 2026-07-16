using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// Strict request-body deserialization at the public trust boundary
/// (SECURITY.md, Input validation rules 1–3), mirroring the B2B API's reader:
/// the body binds only to the dedicated DTO, unknown members are rejected —
/// never stripped into acceptance — numbers are strictly typed, no comments,
/// no trailing commas. Any violation is the generic RFC 9457 rejection with
/// no parser internals, paths, offsets, or echoed values (SECURITY.md,
/// Logging rule 1).
/// </summary>
internal static class ContactRequestReader
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
            return (null, ContactProblems.UnsupportedMediaType());
        }

        try
        {
            var request = await http.Request.ReadFromJsonAsync<T>(Strict, http.RequestAborted);
            return request is null
                ? (null, ContactProblems.Rejected())
                : (request, null);
        }
        catch (JsonException)
        {
            return (null, ContactProblems.Rejected());
        }
        catch (InvalidDataException)
        {
            return (null, ContactProblems.Rejected());
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
