using System.Text.Json;
using System.Text.Json.Serialization;
using BuildCv.Application.Features.Jobs;
using BuildCv.Domain.Resumes;

namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Convierte el cuerpo JSON de <c>POST /api/v1/score</c> al comando
/// discriminado correcto en función de <c>engineVersion</c>. Esta capa de
/// binding vive aquí (no en la API) porque el handler ya trabaja con la
/// unión; el endpoint solo recibe el JSON crudo.
/// </summary>
public sealed class ScoreCvCommandJsonConverter : JsonConverter<ScoreCvCommand>
{
    public override ScoreCvCommand? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("engineVersion", out var versionProp)
            || versionProp.ValueKind != JsonValueKind.String)
        {
            throw new JsonException(
                "Falta 'engineVersion' (esperado \"1.0.0\" o \"2.0.0\").");
        }

        var version = versionProp.GetString();
        var raw = root.GetRawText();

        return version switch
        {
            EngineVersions.V1 => JsonSerializer.Deserialize<TextScoreCommand>(
                raw, options),
            EngineVersions.V2 => JsonSerializer.Deserialize<StructuredScoreCommand>(
                raw, options),
            _ => throw new JsonException(
                $"engineVersion '{version}' desconocido (esperado \"1.0.0\" o \"2.0.0\")."),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ScoreCvCommand value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
