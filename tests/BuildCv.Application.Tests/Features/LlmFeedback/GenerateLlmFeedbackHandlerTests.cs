using BuildCv.Application.Features.Jobs;
using BuildCv.Application.Features.LlmFeedback;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Tests.Features.LlmFeedback;

public sealed class GenerateLlmFeedbackHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDisabledWhenOptionsAreOff()
    {
        var client = new CapturingClient();
        var handler = CreateHandler(client, new LlmFeedbackOptions { Enabled = false });

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        result.ErrorCode.Should().Be("disabled");
        result.StatusCode.Should().Be(403);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_RedactsPiiBeforeProviderBoundary()
    {
        var client = new CapturingClient();
        var handler = CreateHandler(client);

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        result.Response.Should().NotBeNull();
        client.CapturedContext!.RedactedCvText.Should().Contain("[EMAIL_REDACTED]");
        client.CapturedContext.RedactedCvText.Should().Contain("[PHONE_REDACTED]");
        client.CapturedContext.RedactedCvText.Should().NotContain("ada@example.com");
        client.CapturedContext.RedactedJobText.Should().Contain("Backend Engineer");
    }

    [Fact]
    public async Task HandleAsync_PassesConfidenceMarkersAndOptionalScoreContext()
    {
        var client = new CapturingClient();
        var handler = CreateHandler(client);
        var request = CreateRequest(includeScoreContext: false);

        await handler.HandleAsync(request, CancellationToken.None);

        client.CapturedContext!.Request.ScoreContext.Should().BeNull();
        client.CapturedContext.Request.ConfidenceMarkers.Should().ContainKey("basics.name")
            .WhoseValue.Should().Be(ConfidenceMarker.UserConfirmed);
    }

    [Fact]
    public async Task HandleAsync_TimeoutReturnsDegradedFallback()
    {
        var client = new TimeoutClient();
        var handler = CreateHandler(client, new LlmFeedbackOptions { Enabled = true, TimeoutMs = 1, Model = "fake-local-v1" });

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        result.Response.Should().NotBeNull();
        result.Response!.Degraded.Should().BeTrue();
        result.Response.Summary.Should().Be("AI feedback no disponible");
    }

    [Fact]
    public async Task HandleAsync_ProviderUnavailableReturnsDegradedFallbackWithEmptyArrays()
    {
        var handler = CreateHandler(new ThrowingClient());

        var result = await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        result.Response.Should().NotBeNull();
        result.Response!.Degraded.Should().BeTrue();
        result.Response.Strengths.Should().BeEmpty();
        result.Response.Risks.Should().BeEmpty();
        result.Response.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_RedactionFailureDoesNotCallProvider()
    {
        var client = new CapturingClient();
        var handler = CreateHandler(client);

        var result = await handler.HandleAsync(CreateRequest(summary: new string('a', PiiRedactor.MaxInputCharacters + 1)), CancellationToken.None);

        result.ErrorCode.Should().Be("redaction_failure");
        result.StatusCode.Should().Be(500);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_LogsMetadataOnlyAndDegradedReasonLatencyTraceId()
    {
        var logger = new CapturingLogger<GenerateLlmFeedbackHandler>();
        var handler = CreateHandler(new ThrowingClient(), logger: logger);

        await handler.HandleAsync(CreateRequest(), CancellationToken.None);

        logger.Messages.Should().Contain(message => message.Contains("LlmFeedback request", StringComparison.Ordinal));
        logger.Messages.Should().Contain(message => message.Contains("LlmFeedback degraded", StringComparison.Ordinal));
        logger.Messages.Should().Contain(message => message.Contains("reason=provider_unavailable", StringComparison.Ordinal));
        logger.Messages.Should().Contain(message => message.Contains("latencyMs=", StringComparison.Ordinal));
        logger.Messages.Should().Contain(message => message.Contains("traceId=", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("ada@example.com", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("Secret CV token", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("Secret job token", StringComparison.Ordinal));
    }

    private static GenerateLlmFeedbackHandler CreateHandler(
        ILlmFeedbackClient client,
        LlmFeedbackOptions? options = null,
        ILogger<GenerateLlmFeedbackHandler>? logger = null) =>
        new(
            client,
            options ?? new LlmFeedbackOptions { Enabled = true, TimeoutMs = 5000, Model = "fake-local-v1" },
            new FixedClock(),
            logger ?? new CapturingLogger<GenerateLlmFeedbackHandler>());

    private static LlmFeedbackRequest CreateRequest(bool includeScoreContext = true, string? summary = null) =>
        new(
            new CvDocument(
                new Basics(
                    "Ada Lovelace",
                    "ada@example.com",
                    "+57 300 123 4567",
                    "Calle 80 # 12-34 Bogotá",
                    "https://personal.example.com",
                    [],
                    summary ?? "Secret CV token with .NET APIs",
                    null,
                    new BasicsConfidence(
                        ConfidenceMarker.UserConfirmed,
                        ConfidenceMarker.Explicit,
                        ConfidenceMarker.Explicit,
                        ConfidenceMarker.Inferred,
                        ConfidenceMarker.Inferred,
                        ConfidenceMarker.Inferred,
                        ConfidenceMarker.Explicit,
                        ConfidenceMarker.Inferred)),
                [],
                [],
                [new TaggedResumeSkill(new ResumeSkillEntry(".NET", "Advanced"), new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred))],
                [],
                [],
                [],
                new CvMeta("2.0.0")),
            new JobSpec("Backend Engineer", "BuildCv", "Secret job token", "Remote", EmploymentType.FullTime, [".NET", "PostgreSQL"]),
            null,
            null,
            includeScoreContext ? new LlmFeedbackScoreContext(82, PerSectionScore.Zero.WithSkills(90), "2.0.0") : null,
            new Dictionary<string, ConfidenceMarker> { ["basics.name"] = ConfidenceMarker.UserConfirmed },
            true);

    private sealed class CapturingClient : ILlmFeedbackClient
    {
        public int Calls { get; private set; }

        public LlmFeedbackContext? CapturedContext { get; private set; }

        public Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default)
        {
            Calls++;
            CapturedContext = context;
            return Task.FromResult(new LlmFeedbackResponse("ok", ["s"], ["r"], [], [], [], "fake", "fake-local-v1", DateTimeOffset.UnixEpoch, false));
        }
    }

    private sealed class ThrowingClient : ILlmFeedbackClient
    {
        public Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default) =>
            throw new InvalidOperationException("provider failed with Secret CV token");
    }

    private sealed class TimeoutClient : ILlmFeedbackClient
    {
        public async Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new LlmFeedbackResponse("late", [], [], [], [], [], "fake", "fake-local-v1", DateTimeOffset.UnixEpoch, false);
        }
    }

    private sealed class FixedClock : ILlmFeedbackClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.UnixEpoch;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
