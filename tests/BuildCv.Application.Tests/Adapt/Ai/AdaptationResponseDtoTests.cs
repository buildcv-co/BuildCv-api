using System.ComponentModel.DataAnnotations;
using BuildCv.Application.Features.Adapt;
using FluentAssertions;
using Xunit;

namespace BuildCv.Application.Tests.Adapt.Ai;

/// <summary>
/// Tests para el DTO AdaptationResponse — contrato estricto entre la IA y el handler.
/// Constitution Art. I: cero invención. Constitution Art. VI: contrato tipado en Application.
/// </summary>
public sealed class AdaptationResponseDtoTests
{
    [Fact]
    public void Should_validate_when_all_required_fields_present()
    {
        var response = new AdaptationResponse
        {
            AdaptedText = "CV optimizado",
            Reasoning = "Reordené skills para destacar C#",
            AddedEntities = Array.Empty<string>(),
            RemovedEntities = Array.Empty<string>()
        };

        var results = Validate(response);
        results.Should().BeEmpty();
    }

    [Fact]
    public void Should_fail_validation_when_adapted_text_is_null()
    {
        var response = new AdaptationResponse
        {
            AdaptedText = null!,
            Reasoning = "razonamiento",
            AddedEntities = Array.Empty<string>(),
            RemovedEntities = Array.Empty<string>()
        };

        var results = Validate(response);
        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(AdaptationResponse.AdaptedText)));
    }

    [Fact]
    public void Should_fail_validation_when_adapted_text_is_empty()
    {
        var response = new AdaptationResponse
        {
            AdaptedText = "",
            Reasoning = "razonamiento",
            AddedEntities = Array.Empty<string>(),
            RemovedEntities = Array.Empty<string>()
        };

        var results = Validate(response);
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void Should_fail_validation_when_reasoning_is_null()
    {
        var response = new AdaptationResponse
        {
            AdaptedText = "texto",
            Reasoning = null!,
            AddedEntities = Array.Empty<string>(),
            RemovedEntities = Array.Empty<string>()
        };

        var results = Validate(response);
        results.Should().Contain(r => r.MemberNames.Contains(nameof(AdaptationResponse.Reasoning)));
    }

    [Fact]
    public void Should_allow_empty_added_and_removed_entities()
    {
        var response = new AdaptationResponse
        {
            AdaptedText = "texto",
            Reasoning = "razonamiento",
            AddedEntities = Array.Empty<string>(),
            RemovedEntities = Array.Empty<string>()
        };

        var results = Validate(response);
        results.Should().BeEmpty();
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var ctx = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, ctx, results, validateAllProperties: true);
        return results;
    }
}
