using System.Net;
using System.Net.Http.Json;
using BuildCv.Application.Features.LlmFeedback;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildCv.Api.IntegrationTests;

public sealed class LlmFeedbackEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PostFeedback_WithValidBody_Returns200AndFeedbackContract()
    {
        var client = CreateClient(enabled: true);

        var response = await client.PostAsJsonAsync("/api/v1/llm/feedback", CreatePayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LlmFeedbackResponseDto>();
        body.Should().NotBeNull();
        body!.Provider.Should().Be("fake");
        body.Model.Should().Be("fake-local-v1");
        body.Degraded.Should().BeFalse();
        body.Summary.Should().Contain("2.0.0");
        body.Strengths.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostFeedback_MissingCv_Returns400ValidationError()
    {
        var client = CreateClient(enabled: true);

        var response = await client.PostAsJsonAsync("/api/v1/llm/feedback", new { job = CreateJob() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("validation_error");
    }

    [Fact]
    public async Task PostFeedback_WhenDisabled_Returns403Disabled()
    {
        var client = CreateClient(enabled: false);

        var response = await client.PostAsJsonAsync("/api/v1/llm/feedback", CreatePayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("disabled");
    }

    [Fact]
    public async Task PostFeedback_RateLimit_Returns429AndRetryAfter()
    {
        var client = CreateClient(enabled: true, requestsPerWindow: 1, windowSeconds: 60);

        var first = await client.PostAsJsonAsync("/api/v1/llm/feedback", CreatePayload());
        var second = await client.PostAsJsonAsync("/api/v1/llm/feedback", CreatePayload());

        first.StatusCode.Should().BeOneOf(HttpStatusCode.OK, (HttpStatusCode)429);
        second.StatusCode.Should().Be((HttpStatusCode)429);
        second.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task PostFeedback_RedactionFailure_Returns500RedactionFailure()
    {
        var client = CreateClient(enabled: true);

        var response = await client.PostAsJsonAsync("/api/v1/llm/feedback", CreatePayload(summary: new string('a', 100_001)));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("redaction_failure");
    }

    [Fact]
    public async Task PostFeedback_WithMinimaxProvider_Returns200AndMinimaxContract()
    {
        var client = CreateClientWithProvider(new EndpointFakeClient(new LlmFeedbackResponse(
            "MiniMax summary",
            ["Provider success"],
            [],
            [],
            [],
            [],
            "minimax",
            "MiniMax-M2.7",
            DateTimeOffset.UnixEpoch,
            false)));

        var response = await client.PostAsJsonAsync("/api/v1/llm/feedback", CreatePayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LlmFeedbackResponseDto>();
        body!.Provider.Should().Be("minimax");
        body.Model.Should().Be("MiniMax-M2.7");
        body.Degraded.Should().BeFalse();
    }

    [Fact]
    public async Task PostFeedback_WithProviderRateLimit_PreservesRetryAfter()
    {
        var client = CreateClientWithProvider(new RateLimitedEndpointClient(TimeSpan.FromSeconds(42)));

        var response = await client.PostAsJsonAsync("/api/v1/llm/feedback", CreatePayload());

        response.StatusCode.Should().Be((HttpStatusCode)429);
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(42));
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("rate_limited");
    }

    private HttpClient CreateClient(bool enabled, int requestsPerWindow = 30, int windowSeconds = 60)
    {
        var client = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmFeedback:Enabled"] = enabled.ToString(),
                ["LlmFeedback:Provider"] = "fake",
                ["LlmFeedback:Model"] = "fake-local-v1",
                ["LlmFeedback:TimeoutMs"] = "5000",
                ["LlmFeedback:RedactionEnabled"] = "true",
                ["LlmFeedback:RateLimit:RequestsPerWindow"] = requestsPerWindow.ToString(),
                ["LlmFeedback:RateLimit:WindowSeconds"] = windowSeconds.ToString(),
            });
        })).CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"203.0.113.{Random.Shared.Next(1, 255)}");
        return client;
    }

    private HttpClient CreateClientWithProvider(ILlmFeedbackClient feedbackClient)
    {
        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LlmFeedback:Enabled"] = "true",
                    ["LlmFeedback:Provider"] = "minimax",
                    ["LlmFeedback:ApiKey"] = "test-provider-key",
                    ["LlmFeedback:BaseUrl"] = "https://provider.test/anthropic",
                    ["LlmFeedback:Model"] = "MiniMax-M2.7",
                    ["LlmFeedback:TimeoutMs"] = "5000",
                    ["LlmFeedback:MaxInputLength"] = "32000",
                    ["LlmFeedback:MaxOutputTokens"] = "1024",
                    ["LlmFeedback:RedactionEnabled"] = "true",
                    ["LlmFeedback:RateLimit:RequestsPerWindow"] = "30",
                    ["LlmFeedback:RateLimit:WindowSeconds"] = "60",
                });
            });
            builder.ConfigureServices(services => services.AddSingleton(feedbackClient));
        }).CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"198.51.100.{Random.Shared.Next(1, 255)}");
        return client;
    }

    private static object CreatePayload(string summary = "Backend engineer with .NET and email ada@example.com") => new
    {
        cv = new
        {
            basics = new
            {
                name = "Ada Lovelace",
                email = "ada@example.com",
                phone = "+57 300 123 4567",
                location = "Bogotá",
                url = "https://personal.example.com",
                profiles = Array.Empty<object>(),
                summary,
                confidence = new
                {
                    name = "userConfirmed",
                    email = "explicit",
                    phone = "explicit",
                    location = "inferred",
                    url = "inferred",
                    profiles = "inferred",
                    summary = "explicit",
                    datosPersonales = "inferred",
                },
            },
            work = Array.Empty<object>(),
            education = Array.Empty<object>(),
            skills = new[]
            {
                new { entry = new { name = ".NET", level = "Advanced" }, confidence = new { name = "explicit", level = "inferred" } },
            },
            projects = Array.Empty<object>(),
            certificates = Array.Empty<object>(),
            languages = Array.Empty<object>(),
            meta = new { engineVersion = "2.0.0" },
        },
        job = CreateJob(),
        scoreContext = new { score = 82, components = new { skills = 90 }, version = "2.0.0" },
        confidenceMarkers = new Dictionary<string, string> { ["basics.name"] = "userConfirmed" },
        sessionToggleState = true,
    };

    private static object CreateJob() => new
    {
        title = "Backend Engineer",
        company = "BuildCv",
        description = "Build APIs with Secret job token",
        location = "Remote",
        employmentType = "fullTime",
        requirements = new[] { ".NET", "PostgreSQL" },
    };

    private sealed record LlmFeedbackResponseDto(
        string Summary,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> Risks,
        IReadOnlyList<object> Suggestions,
        IReadOnlyList<string> MissingKeywords,
        IReadOnlyList<string> Questions,
        string Provider,
        string Model,
        DateTimeOffset GeneratedAt,
        bool Degraded);

    private sealed class EndpointFakeClient(LlmFeedbackResponse response) : ILlmFeedbackClient
    {
        public Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default) =>
            Task.FromResult(response);
    }

    private sealed class RateLimitedEndpointClient(TimeSpan retryAfter) : ILlmFeedbackClient
    {
        public Task<LlmFeedbackResponse> GenerateAsync(LlmFeedbackContext context, CancellationToken ct = default) =>
            throw new LlmFeedbackRateLimitedException(retryAfter);
    }
}
