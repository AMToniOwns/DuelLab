using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DuelLab.Application.Common;

/// <summary>The JSON settings used for stored data and the HTTP API, so both look the same.</summary>
public static class JsonDefaults
{
    /// <summary>camelCase names and enums as strings.</summary>
    public static readonly JsonSerializerOptions Options = Create();

    /// <summary>Applies DuelLab's settings to an existing options object (used by ASP.NET Core).</summary>
    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        Apply(options);
        options.MakeReadOnly();
        return options;
    }
}
