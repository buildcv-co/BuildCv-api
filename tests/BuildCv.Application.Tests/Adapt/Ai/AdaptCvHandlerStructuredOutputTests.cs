using BuildCv.Application.Features.Adapt;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Lexicon;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BuildCv.Application.Tests.Adapt.Ai;

/// <summary>
/// Tests de integración: AdaptCvHandler debe usar CompleteStructuredAsync&lt;AdaptationResponse&gt;,
/// no CompleteAsync(string). El handler debe propagar el AdaptedText tipado al flujo de validación.
/// Constitution Art. I: cero invención — el handler sigue validando entidades cruzadas.
/// Constitution Art. III: sin logs de contenido.
/// </summary>
public sealed class AdaptCvHandlerStructuredOutputTests
{
    private readonly StructuredFakeAiClient _ai = new();
    private readonly EntityExtractor _extractor;
    private readonly CrossEntityValidator _crossValidator = new();
    private readonly SeverityPolicy _severityPolicy = new();
    private readonly PromptBuilder _promptBuilder = new();
    private readonly AdaptCvHandler _handler;

    public AdaptCvHandlerStructuredOutputTests()
    {
        _extractor = new EntityExtractor(new TestGazetteer());
        _handler = new AdaptCvHandler(_ai, _extractor, _crossValidator, _severityPolicy, _promptBuilder, NullLogger<AdaptCvHandler>.Instance);
    }

    [Fact]
    public async Task Should_call_complete_structured_async_with_adaptation_response()
    {
        _ai.StructuredResponse = new AdaptationResponse
        {
            AdaptedText = "I worked at RealCorp with C#",
            Reasoning = "Reordered skills",
            AddedEntities = Array.Empty<string>(),
            RemovedEntities = Array.Empty<string>()
        };

        var result = await _handler.Handle(
            new AdaptCvCommand("I worked at RealCorp with C#", "Looking for C# dev"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _ai.LastStructuredCallType.Should().Be(typeof(AdaptationResponse));
    }

    [Fact]
    public async Task Should_use_adapted_text_from_typed_response_not_raw_string()
    {
        _ai.StructuredResponse = new AdaptationResponse
        {
            AdaptedText = "TYPED ADAPTED CV",
            Reasoning = "Reasoning from IA",
            AddedEntities = Array.Empty<string>(),
            RemovedEntities = Array.Empty<string>()
        };

        var result = await _handler.Handle(
            new AdaptCvCommand("I worked at RealCorp with C#", "Looking for C# dev"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdaptedCv.Should().Be("TYPED ADAPTED CV");
    }

    [Fact]
    public async Task Should_still_detect_invention_through_typed_response()
    {
        _ai.StructuredResponse = new AdaptationResponse
        {
            AdaptedText = "I worked at FakeCorp with C# and AWS",
            Reasoning = "Added keywords",
            AddedEntities = new[] { "FakeCorp", "AWS" },
            RemovedEntities = Array.Empty<string>()
        };

        var result = await _handler.Handle(
            new AdaptCvCommand("I worked at RealCorp with C#", "Looking for C# dev"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Validation.Inventions.Should().NotBeEmpty();
    }
}

internal sealed class StructuredFakeAiClient : IAiClient
{
    public AdaptationResponse? StructuredResponse { get; set; }
    public Type? LastStructuredCallType { get; private set; }

    public Task<T> CompleteStructuredAsync<T>(string prompt, CancellationToken ct) where T : class
    {
        LastStructuredCallType = typeof(T);
        if (StructuredResponse is T typed)
        {
            return Task.FromResult(typed);
        }
        throw new InvalidOperationException($"Test fake does not have a {typeof(T).Name} configured");
    }

    public Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        return Task.FromResult(StructuredResponse?.AdaptedText ?? "fallback");
    }
}

internal sealed class TestGazetteer : ISkillGazetteer
{
    public string Version => "test";

    public bool TryResolve(string normalizedToken, out SkillEntry entry) { entry = null!; return false; }
    public bool TryGetById(string canonicalId, out SkillEntry entry) { entry = null!; return false; }
    public IReadOnlyList<string> Related(string canonicalId) => Array.Empty<string>();
    public IReadOnlyList<string> Implies(string canonicalId) => Array.Empty<string>();
    public bool AreConfusable(string a, string b) => false;
}
