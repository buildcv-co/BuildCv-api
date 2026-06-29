using System.Net;
using System.Text;
using System.Text.Json;
using BuildCv.Application.Features.Jobs;
using BuildCv.Application.Features.LlmFeedback;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using BuildCv.Infrastructure.LlmFeedback;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.LlmFeedback;

public sealed class MinimaxLlmFeedbackClientTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateAsync_PostsAnthropicTextOnlyRequestWithSafeHeadersAndRedactedPayload()
    {
        var handler = new CapturingHandler(OkResponse());
        var client = CreateClient(handler, options =>
        {
            options.ApiKey = "test-provider-key";
            options.Model = "MiniMax-M2.7";
            options.MaxOutputTokens = 777;
        });

        await client.GenerateAsync(CreateContext(redactedCv: "CV [EMAIL_REDACTED] .NET", redactedJob: "Job PostgreSQL"));

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("https://provider.test/anthropic/v1/messages");
        handler.Request.Headers.GetValues("x-api-key").Should().ContainSingle("test-provider-key");
        handler.Request.Headers.GetValues("anthropic-version").Should().ContainSingle("2023-06-01");
        handler.Request.Headers.Authorization.Should().BeNull();
        handler.Request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        handler.Body.Should().NotContain("ada@example.com");

        using var body = JsonDocument.Parse(handler.Body);
        body.RootElement.GetProperty("model").GetString().Should().Be("MiniMax-M2.7");
        body.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(777);
        body.RootElement.TryGetProperty("stream", out _).Should().BeFalse();
        body.RootElement.TryGetProperty("tools", out _).Should().BeFalse();
        body.RootElement.TryGetProperty("messages", out var messages).Should().BeTrue();
        messages[0].GetProperty("role").GetString().Should().Be("user");
        messages[0].GetProperty("content")[0].GetProperty("type").GetString().Should().Be("text");
        messages[0].GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("CV [EMAIL_REDACTED] .NET");
    }

    [Fact]
    public async Task GenerateAsync_ValidResponseMapsTenFieldContract()
    {
        var client = CreateClient(new CapturingHandler(OkResponse()));

        var response = await client.GenerateAsync(CreateContext());

        response.Summary.Should().Be("Strong fit");
        response.Strengths.Should().Contain("Clear .NET evidence");
        response.Risks.Should().Contain("Missing PostgreSQL proof");
        response.Suggestions.Should().ContainSingle(s => s.Category == "keywords" && s.Severity == LlmFeedbackSeverity.Medium);
        response.MissingKeywords.Should().Contain("PostgreSQL");
        response.Questions.Should().Contain("Have you used PostgreSQL in production?");
        response.Provider.Should().Be("minimax");
        response.Model.Should().Be("MiniMax-M2.7");
        response.GeneratedAt.Should().Be(FixedNow);
        response.Degraded.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"content\":[]}")]
    [InlineData("{\"content\":[{\"type\":\"tool_use\",\"name\":\"x\"}]}")]
    public async Task GenerateAsync_MalformedResponseReturnsDegradedFallback(string providerBody)
    {
        var client = CreateClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(providerBody, Encoding.UTF8, "application/json")
        }));

        var response = await client.GenerateAsync(CreateContext());

        response.Degraded.Should().BeTrue();
        response.Summary.Should().Be("AI feedback no disponible");
        response.Strengths.Should().BeEmpty();
        response.Provider.Should().Be("minimax");
    }

    [Fact]
    public async Task GenerateAsync_IgnoresReasoningBlocksAndUsesTextBlock()
    {
        var body = """
        {"content":[{"type":"thinking","thinking":"secret"},{"type":"text","text":"{\"summary\":\"Usable\",\"strengths\":[\"A\"],\"risks\":[],\"suggestions\":[],\"missingKeywords\":[],\"questions\":[]}"}],"model":"MiniMax-M2.7"}
        """;
        var client = CreateClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));

        var response = await client.GenerateAsync(CreateContext());

        response.Degraded.Should().BeFalse();
        response.Summary.Should().Be("Usable");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GenerateAsync_AuthFailuresThrowUnavailableWithoutKeyLeak(HttpStatusCode statusCode)
    {
        var client = CreateClient(new CapturingHandler(ErrorResponse(statusCode, "bad test-provider-key")));

        var action = () => client.GenerateAsync(CreateContext());

        var exception = await action.Should().ThrowAsync<LlmFeedbackUnavailableException>();
        exception.Which.Message.Should().NotContain("test-provider-key");
    }

    [Fact]
    public async Task GenerateAsync_RateLimitPreservesRetryAfter()
    {
        var response = ErrorResponse(HttpStatusCode.TooManyRequests, "limit");
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
        var client = CreateClient(new CapturingHandler(response));

        var action = () => client.GenerateAsync(CreateContext());

        var exception = await action.Should().ThrowAsync<LlmFeedbackRateLimitedException>();
        exception.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(42));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GenerateAsync_ServerFailuresThrowUnavailable(HttpStatusCode statusCode)
    {
        var client = CreateClient(new CapturingHandler(ErrorResponse(statusCode, "raw provider body")));

        var action = () => client.GenerateAsync(CreateContext());

        await action.Should().ThrowAsync<LlmFeedbackUnavailableException>();
    }

    [Fact]
    public async Task GenerateAsync_NetworkFailureThrowsUnavailable()
    {
        var client = CreateClient(new ThrowingHandler(new HttpRequestException("dns failed with test-provider-key")));

        var action = () => client.GenerateAsync(CreateContext());

        var exception = await action.Should().ThrowAsync<LlmFeedbackUnavailableException>();
        exception.Which.Message.Should().NotContain("test-provider-key");
    }

    [Fact]
    public async Task GenerateAsync_OverMaxInputLengthThrowsBeforeHttpCall()
    {
        var handler = new CapturingHandler(OkResponse());
        var client = CreateClient(handler, options => options.MaxInputLength = 10);

        var action = () => client.GenerateAsync(CreateContext(redactedCv: new string('a', 11), redactedJob: "b"));

        await action.Should().ThrowAsync<LlmFeedbackValidationException>();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task GenerateAsync_CancelledRequestThrowsTimeoutException()
    {
        var client = CreateClient(new DelayingHandler());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        var action = () => client.GenerateAsync(CreateContext(), cts.Token);

        await action.Should().ThrowAsync<LlmFeedbackTimeoutException>();
    }

    [Fact]
    public async Task GenerateAsync_LogsMetadataOnly()
    {
        var logger = new CapturingLogger<MinimaxLlmFeedbackClient>();
        var client = CreateClient(new CapturingHandler(OkResponse()), logger: logger);

        await client.GenerateAsync(CreateContext(redactedCv: "Secret CV token [EMAIL_REDACTED]", redactedJob: "Secret job token"));

        logger.Messages.Should().Contain(message => message.Contains("provider=minimax", StringComparison.Ordinal));
        logger.Messages.Should().Contain(message => message.Contains("model=MiniMax-M2.7", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("Secret CV token", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("Secret job token", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("test-provider-key", StringComparison.Ordinal));
        logger.Messages.Should().NotContain(message => message.Contains("Strong fit", StringComparison.Ordinal));
    }

    private static MinimaxLlmFeedbackClient CreateClient(
        HttpMessageHandler handler,
        Action<LlmFeedbackOptions>? configure = null,
        ILogger<MinimaxLlmFeedbackClient>? logger = null)
    {
        var options = new LlmFeedbackOptions
        {
            Provider = "minimax",
            BaseUrl = "https://provider.test/anthropic",
            ApiKey = "test-provider-key",
            Model = "MiniMax-M2.7",
            TimeoutMs = 5000,
            MaxInputLength = 32000,
            MaxOutputTokens = 1024,
        };
        configure?.Invoke(options);
        return new MinimaxLlmFeedbackClient(
            new HttpClient(handler),
            Options.Create(options),
            new FixedClock(),
            logger ?? new CapturingLogger<MinimaxLlmFeedbackClient>());
    }

    private static LlmFeedbackContext CreateContext(string redactedCv = "CV .NET", string redactedJob = "Job PostgreSQL") =>
        new(CreateRequest(), redactedCv, redactedJob);

    private static LlmFeedbackRequest CreateRequest()
    {
        var cv = new CvDocument(
            new Basics("Ada Lovelace", "ada@example.com", null, null, null, [], "Backend engineer", null,
                new BasicsConfidence(ConfidenceMarker.UserConfirmed, ConfidenceMarker.Explicit, ConfidenceMarker.Inferred, ConfidenceMarker.Inferred, ConfidenceMarker.Inferred, ConfidenceMarker.Inferred, ConfidenceMarker.Explicit, ConfidenceMarker.Inferred)),
            [], [], [new TaggedResumeSkill(new ResumeSkillEntry(".NET", "Advanced"), new SkillConfidence(ConfidenceMarker.Explicit, ConfidenceMarker.Inferred))], [], [], [], new CvMeta("2.0.0"));
        return new LlmFeedbackRequest(
            cv,
            new JobSpec("Backend Engineer", "BuildCv", "APIs", "Remote", EmploymentType.FullTime, ["PostgreSQL"]),
            null,
            null,
            new LlmFeedbackScoreContext(82, PerSectionScore.Zero.WithSkills(90), "2.0.0"),
            new Dictionary<string, ConfidenceMarker>(),
            true);
    }

    private static HttpResponseMessage OkResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"content":[{"type":"text","text":"{\"summary\":\"Strong fit\",\"strengths\":[\"Clear .NET evidence\"],\"risks\":[\"Missing PostgreSQL proof\"],\"suggestions\":[{\"category\":\"keywords\",\"text\":\"Add PostgreSQL proof if accurate.\",\"severity\":\"medium\"}],\"missingKeywords\":[\"PostgreSQL\"],\"questions\":[\"Have you used PostgreSQL in production?\"]}"}],"model":"MiniMax-M2.7"}
            """, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage ErrorResponse(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class DelayingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return OkResponse();
        }
    }

    private sealed class FixedClock : ILlmFeedbackClock
    {
        public DateTimeOffset UtcNow { get; } = FixedNow;
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
