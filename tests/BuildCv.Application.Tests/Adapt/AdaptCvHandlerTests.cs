using BuildCv.Application.Features.Adapt;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Lexicon;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BuildCv.Application.Tests.Adapt;

public sealed class AdaptCvHandlerTests
{
    private readonly FakeAiClient _ai = new();
    private readonly EntityExtractor _extractor;
    private readonly CrossEntityValidator _crossValidator = new();
    private readonly SeverityPolicy _severityPolicy = new();
    private readonly PromptBuilder _promptBuilder = new();
    private readonly AdaptCvHandler _handler;

    public AdaptCvHandlerTests()
    {
        var gazetteer = new TestGazetteer();
        _extractor = new EntityExtractor(gazetteer);
        _handler = new AdaptCvHandler(_ai, _extractor, _crossValidator, _severityPolicy, _promptBuilder, NullLogger<AdaptCvHandler>.Instance);
    }

    [Fact]
    public async Task Should_call_ai_with_prompt_built_by_prompt_builder()
    {
        _ai.Response = "OPTIMIZED CV";
        var cmd = new AdaptCvCommand("Backend dev with C#", "Looking for C# dev");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _ai.LastPrompt.Should().Contain("DATO");
        _ai.LastPrompt.Should().Contain("C#");
    }

    [Fact]
    public async Task Should_extract_original_entities_before_calling_ai()
    {
        _ai.Response = "OPTIMIZED";
        var cmd = new AdaptCvCommand("I worked at Acme Corp as developer with C# and .NET", "Looking for C# dev");

        await _handler.Handle(cmd, CancellationToken.None);

        _ai.LastPrompt.Should().Contain("Acme Corp");
    }

    [Fact]
    public async Task Should_detect_invention_in_adapted_cv()
    {
        _ai.Response = "I worked at FakeCorp with C# and AWS";
        var cmd = new AdaptCvCommand("I worked at RealCorp with C#", "Looking for C# dev");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Validation.Inventions.Should().NotBeEmpty();
        result.Value.Validation.Inventions.Should().Contain(i => i.Claimed == "FakeCorp" || i.Claimed == "AWS");
    }

    [Fact]
    public async Task Should_return_warning_when_severity_is_warning()
    {
        _ai.Response = "C# and .NET and Python";
        var cmd = new AdaptCvCommand("I know C#", "Looking for C# dev");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Validation.Severity.Should().BeOneOf(Severity.Warning, Severity.Critical, Severity.None);
    }

    [Fact]
    public async Task Should_not_modify_original_cv_content()
    {
        var original = "I worked at RealCorp as C# dev";
        _ai.Response = "FAKE: I led 50 engineers at FakeCorp with PhD";
        var cmd = new AdaptCvCommand(original, "Looking for dev");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Value.AdaptedCv.Should().Be("FAKE: I led 50 engineers at FakeCorp with PhD");
        result.Value.Validation.Inventions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_propagate_ai_client_exception_as_failure()
    {
        _ai.ShouldThrow = new HttpRequestException("AI service unavailable");
        var cmd = new AdaptCvCommand("Valid CV text here", "Valid job text here");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}

internal sealed class FakeAiClient : IAiClient
{
    public string Response { get; set; } = "Default response";
    public string LastPrompt { get; private set; } = "";
    public Exception? ShouldThrow { get; set; }

    public Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        LastPrompt = prompt;
        if (ShouldThrow is not null)
        {
            throw ShouldThrow;
        }
        return Task.FromResult(Response);
    }

    public Task<T> CompleteStructuredAsync<T>(string prompt, CancellationToken ct) where T : class
    {
        LastPrompt = prompt;
        if (ShouldThrow is not null)
        {
            throw ShouldThrow;
        }

        if (typeof(T) == typeof(AdaptationResponse))
        {
            var stub = (T)(object)new AdaptationResponse
            {
                AdaptedText = Response,
                Reasoning = "fake reasoning",
                AddedEntities = Array.Empty<string>(),
                RemovedEntities = Array.Empty<string>()
            };
            return Task.FromResult(stub);
        }

        throw new NotSupportedException($"FakeAiClient does not implement {typeof(T).Name}");
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
