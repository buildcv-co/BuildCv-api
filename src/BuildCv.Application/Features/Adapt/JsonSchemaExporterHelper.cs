using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace BuildCv.Application.Features.Adapt;

/// <summary>
/// Pydantic-equivalent en C#: genera un JSON schema desde un tipo C# usando
/// <see cref="JsonSchemaExporter"/> (.NET 9+). El schema se pasa al proveedor de IA
/// como tool input (Anthropic) o response_format.schema (OpenAI-compatible) para
/// garantizar JSON estructurado. Constitution Art. VI: contratos tipados en Application.
/// </summary>
public static class JsonSchemaExporterHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TransformSchemaNode = (_, node) =>
        {
            if (node is not JsonObject obj)
            {
                return node;
            }

            if (obj["type"] is JsonArray typeArray)
            {
                var filtered = new JsonArray();
                foreach (var item in typeArray)
                {
                    if (item is JsonValue v && v.TryGetValue<string>(out var s) && s == "null")
                    {
                        continue;
                    }
                    filtered.Add(item?.DeepClone());
                }

                obj["type"] = filtered.Count switch
                {
                    0 => "object",
                    1 => filtered[0]!.DeepClone(),
                    _ => filtered
                };
            }

            return obj;
        }
    };

    public static string Export<T>() where T : class
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(SerializerOptions, typeof(T), ExporterOptions);
        return node.ToJsonString();
    }
}
