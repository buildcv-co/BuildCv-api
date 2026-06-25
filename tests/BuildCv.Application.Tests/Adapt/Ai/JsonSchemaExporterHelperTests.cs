using System.Text.Json;
using System.Text.Json.Nodes;
using BuildCv.Application.Features.Adapt;
using FluentAssertions;
using Xunit;

namespace BuildCv.Application.Tests.Adapt.Ai;

/// <summary>
/// Tests para JsonSchemaExporterHelper — Pydantic-equivalent en C# (.NET 9+).
/// Genera un JSON schema desde un C# record, removiendo campos que confunden al LLM
/// ($schema, "null" en type arrays) y exponiendo un contrato limpio a Anthropic/Minimax.
/// </summary>
public sealed class JsonSchemaExporterHelperTests
{
    [Fact]
    public void Should_export_valid_json_string_for_type()
    {
        var schema = JsonSchemaExporterHelper.Export<AdaptationResponse>();

        schema.Should().NotBeNullOrEmpty();
        var parsed = JsonNode.Parse(schema);
        parsed.Should().NotBeNull();
    }

    [Fact]
    public void Should_include_required_field_names_in_schema()
    {
        var schema = JsonSchemaExporterHelper.Export<AdaptationResponse>();
        var parsed = JsonNode.Parse(schema)!.AsObject();

        parsed["required"]!.AsArray()
            .Select(n => n!.GetValue<string>())
            .Should()
            .Contain(new[]
            {
                nameof(AdaptationResponse.AdaptedText),
                nameof(AdaptationResponse.Reasoning),
                nameof(AdaptationResponse.AddedEntities),
                nameof(AdaptationResponse.RemovedEntities)
            });
    }

    [Fact]
    public void Should_remove_dollar_schema_field()
    {
        var schema = JsonSchemaExporterHelper.Export<AdaptationResponse>();
        var parsed = JsonNode.Parse(schema)!.AsObject();

        parsed.ContainsKey("$schema").Should().BeFalse();
    }

    [Fact]
    public void Should_not_include_null_in_type_array_for_reference_type()
    {
        var schema = JsonSchemaExporterHelper.Export<AdaptationResponse>();
        var parsed = JsonNode.Parse(schema)!.AsObject();

        var typeField = parsed["type"];
        typeField.Should().NotBeNull();

        if (typeField is JsonArray arr)
        {
            arr.Select(n => n!.GetValue<string>())
                .Should()
                .NotContain("null");
        }
        else if (typeField is JsonValue val)
        {
            val.GetValue<string>().Should().NotBe("null");
        }
    }

    [Fact]
    public void Should_describe_list_property_as_array()
    {
        var schema = JsonSchemaExporterHelper.Export<AdaptationResponse>();
        var parsed = JsonNode.Parse(schema)!.AsObject();
        var properties = parsed["properties"]!.AsObject();

        var addedEntities = properties[nameof(AdaptationResponse.AddedEntities)]!.AsObject();
        var typeNode = addedEntities["type"];

        (typeNode is JsonValue v && v.GetValue<string>() == "array")
            .Should()
            .BeTrue($"AddedEntities must be 'array', got {typeNode}");
    }

    [Fact]
    public void Should_describe_string_property_as_string()
    {
        var schema = JsonSchemaExporterHelper.Export<AdaptationResponse>();
        var parsed = JsonNode.Parse(schema)!.AsObject();
        var properties = parsed["properties"]!.AsObject();

        var adaptedText = properties[nameof(AdaptationResponse.AdaptedText)]!.AsObject();
        var typeNode = adaptedText["type"]!.GetValue<string>();

        typeNode.Should().Be("string");
    }
}
