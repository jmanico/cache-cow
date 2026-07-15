using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CacheCow.Modules.WholesaleB2B.Api.Schema;

/// <summary>
/// First-party, reflection-based JSON Schema generator: schemas derive from
/// the contract records themselves, so validation, documentation, and the
/// running code cannot diverge (CC-API-010; ARCHITECTURE.md, Dependency
/// rule 7). First-party by the dependency policy: no library is needed for
/// this narrow shape (SECURITY.md, Dependency Rules 1–2) — the open question
/// of a richer schema toolchain stays open (issue 053, Open Questions).
///
/// Emitted schemas mirror exactly what the strict deserialization enforces at
/// runtime (CC-API-006): <c>additionalProperties: false</c> (unknown members
/// are rejected, <see cref="JsonUnmappedMemberHandling.Disallow"/>), camelCase
/// property names (web defaults), <c>required</c> for members declared
/// <c>required</c> or bound through a positional record constructor, and
/// strict primitive types (numbers are numbers, never strings). Output is
/// deterministic: properties sort alphabetically.
/// </summary>
public static class B2BJsonSchemaGenerator
{
    public static JsonObject SchemaFor(Type contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return ObjectSchema(contract, [], contract.Name);
    }

    private static JsonObject ObjectSchema(Type type, HashSet<Type> inProgress, string? title = null)
    {
        if (!inProgress.Add(type))
        {
            throw new InvalidOperationException(
                $"Cyclic contract graph at {type.Name}; B2B API contracts must be acyclic.");
        }

        var constructorParameters = type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters())
            .Select(p => p.Name)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var properties = new JsonObject();
        var required = new List<string>();

        foreach (var property in type
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            properties[name] = TypeSchema(property.PropertyType, inProgress);

            var isRequired = property.GetCustomAttribute<RequiredMemberAttribute>() is not null
                || constructorParameters.Contains(property.Name);
            if (isRequired)
            {
                required.Add(name);
            }
        }

        required.Sort(StringComparer.Ordinal);

        var schema = new JsonObject();
        if (title is not null)
        {
            schema["title"] = title;
        }

        schema["type"] = "object";
        schema["additionalProperties"] = false;
        schema["properties"] = properties;
        if (required.Count > 0)
        {
            schema["required"] = new JsonArray([.. required.Select(r => (JsonNode)r)]);
        }

        inProgress.Remove(type);
        return schema;
    }

    private static JsonObject TypeSchema(Type type, HashSet<Type> inProgress)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string))
        {
            return new JsonObject { ["type"] = "string" };
        }

        if (type == typeof(bool))
        {
            return new JsonObject { ["type"] = "boolean" };
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return new JsonObject { ["type"] = "integer" };
        }

        if (type == typeof(DateTimeOffset))
        {
            return new JsonObject { ["type"] = "string", ["format"] = "date-time" };
        }

        if (type.IsEnum)
        {
            return new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray([.. Enum.GetNames(type).Order(StringComparer.Ordinal).Select(n => (JsonNode)n)]),
            };
        }

        var elementType = EnumerableElementType(type);
        if (elementType is not null)
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = TypeSchema(elementType, inProgress),
            };
        }

        if (type.IsClass)
        {
            return ObjectSchema(type, inProgress);
        }

        throw new InvalidOperationException(
            $"Unsupported contract member type {type.Name}; B2B API contracts use only schema-mappable shapes (CC-API-010).");
    }

    private static Type? EnumerableElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            return type.GetGenericArguments()[0];
        }

        return type.IsArray ? type.GetElementType() : null;
    }
}
