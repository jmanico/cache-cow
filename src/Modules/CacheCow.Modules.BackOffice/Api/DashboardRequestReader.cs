using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace CacheCow.Modules.BackOffice.Api;

/// <summary>
/// Strict request reading at the dashboard's trust boundary (SECURITY.md,
/// Input validation rules 1–3). Bodies bind only to the dedicated contract
/// records under options that reject rather than absorb: unknown members are
/// rejected (never silently stripped into acceptance), numbers are strictly
/// typed, and comments and trailing commas are refused. Every violation is a
/// generic 400 — no parser internals, JSON paths, or byte offsets leak
/// (SECURITY.md, Logging rule 1).
///
/// Query parameters are read EXPLICITLY, one named parameter at a time, rather
/// than through a model binder: the dashboard's queries are closed typed
/// shapes with no user-chosen sort or filter column (SECURITY.md, Input
/// validation rule 4), and explicit reads keep it that way.
/// </summary>
internal static class DashboardRequestReader
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>Reads and validates a JSON body, or returns the rejection to send.</summary>
    internal static async Task<(T? Request, IResult? Rejection)> ReadAsync<T>(HttpContext http)
        where T : class
    {
        if (!HasJsonContentType(http))
        {
            return (null, DashboardProblems.UnsupportedMediaType());
        }

        try
        {
            var request = await http.Request.ReadFromJsonAsync<T>(Strict, http.RequestAborted);
            return request is null ? (null, DashboardProblems.Validation()) : (request, null);
        }
        catch (JsonException)
        {
            return (null, DashboardProblems.Validation());
        }
        catch (InvalidDataException)
        {
            return (null, DashboardProblems.Validation());
        }
    }

    /// <summary>Exactly application/json (parameters permitted); anything else is 415 (SECURITY.md, HTTP boundary rule 6).</summary>
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

    /// <summary>
    /// The single value of a query parameter, or null when absent. A REPEATED
    /// parameter is rejected (<paramref name="malformed"/>) rather than
    /// resolved by picking one: duplicate-parameter handling is a classic
    /// parser-differential trick, and there is no correct choice to make
    /// (SECURITY.md, Input validation rule 1).
    /// </summary>
    internal static string? Single(HttpRequest request, string name, ref bool malformed)
    {
        if (!request.Query.TryGetValue(name, out var values) || values.Count == 0)
        {
            return null;
        }

        if (values.Count > 1)
        {
            malformed = true;
            return null;
        }

        var value = values[0];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>An optional int query parameter; unparseable values are malformed, never defaulted.</summary>
    internal static int? OptionalInt(HttpRequest request, string name, ref bool malformed)
    {
        var raw = Single(request, name, ref malformed);
        if (raw is null)
        {
            return null;
        }

        if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            malformed = true;
            return null;
        }

        return value;
    }

    /// <summary>An optional launch market (CC-MKT-001); an unknown code is malformed, never coerced.</summary>
    internal static Market? OptionalMarket(HttpRequest request, string name, ref bool malformed)
    {
        var raw = Single(request, name, ref malformed);
        if (raw is null)
        {
            return null;
        }

        if (!Market.TryParse(raw, out var market))
        {
            malformed = true;
            return null;
        }

        return market;
    }

    /// <summary>An optional RFC 3339 / ISO 8601 timestamp; anything else is malformed.</summary>
    internal static DateTimeOffset? OptionalTimestamp(HttpRequest request, string name, ref bool malformed)
    {
        var raw = Single(request, name, ref malformed);
        if (raw is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
        {
            malformed = true;
            return null;
        }

        return value;
    }
}
