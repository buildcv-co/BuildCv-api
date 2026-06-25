using BuildCv.Application.Features.Adapt;
using BuildCv.Infrastructure.Ai;
using FluentAssertions;
using Xunit;

namespace BuildCv.Application.Tests.Adapt.Ai;

/// <summary>
/// Tests para StubAiClient con structured output — Pydantic-equivalent en C#.
/// Verifica que el stub devuelve un DTO tipado y validable, no un string opaco.
/// </summary>
public sealed class StubAiClientStructuredOutputTests
{
    private readonly StubAiClient _client = new();

    [Fact]
    public async Task CompleteStructuredAsync_should_return_typed_adaptation_response()
    {
        var result = await _client.CompleteStructuredAsync<AdaptationResponse>("prompt", CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeOfType<AdaptationResponse>();
    }

    [Fact]
    public async Task CompleteStructuredAsync_adaptation_response_passes_data_annotations_validation()
    {
        var result = await _client.CompleteStructuredAsync<AdaptationResponse>("prompt", CancellationToken.None);

        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(result);
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(result, ctx, errors, validateAllProperties: true);

        isValid.Should().BeTrue("the stub must produce a contractually valid DTO. Errors: {0}", string.Join("; ", errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task CompleteStructuredAsync_adaptation_response_has_non_empty_adapted_text()
    {
        var result = await _client.CompleteStructuredAsync<AdaptationResponse>("prompt", CancellationToken.None);

        result.AdaptedText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteStructuredAsync_should_throw_for_unsupported_type()
    {
        var act = () => _client.CompleteStructuredAsync<UnsupportedDto>("prompt", CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task CompleteAsync_should_still_return_string_for_backwards_compatibility()
    {
        var result = await _client.CompleteAsync("prompt", CancellationToken.None);

        result.Should().NotBeNullOrEmpty();
    }

    private sealed record UnsupportedDto
    {
        public required string Foo { get; init; }
    }
}
