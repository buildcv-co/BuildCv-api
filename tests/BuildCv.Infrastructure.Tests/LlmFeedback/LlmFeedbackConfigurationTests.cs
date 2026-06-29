using BuildCv.Application.Features.LlmFeedback;
using BuildCv.Infrastructure.LlmFeedback;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.LlmFeedback;

public sealed class LlmFeedbackConfigurationTests
{
    [Fact]
    public void Appsettings_LoadLlmFeedbackDefaultsFromDedicatedNamespace()
    {
        var apiProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/BuildCv.Api"));
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiProject, "appsettings.json"))
            .Build();
        var services = new ServiceCollection();

        configuration.GetSection("LlmFeedback").Exists().Should().BeTrue();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<LlmFeedbackOptions>>().Value;
        options.Enabled.Should().BeFalse();
        options.Provider.Should().Be("fake");
        options.BaseUrl.Should().Be("https://api.minimax.io/anthropic");
        options.ApiKey.Should().BeEmpty();
        options.Model.Should().Be("MiniMax-M2.7");
        options.TimeoutMs.Should().Be(5000);
        options.MaxInputLength.Should().Be(32000);
        options.MaxOutputTokens.Should().Be(1024);
        options.RateLimit.RequestsPerWindow.Should().Be(30);
        options.RateLimit.WindowSeconds.Should().Be(60);
        options.RedactionEnabled.Should().BeTrue();
    }

    [Fact]
    public void LlmFeedbackOptions_BindFromLlmFeedbackSectionWithOfflineDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmFeedback:Enabled"] = "false",
                ["LlmFeedback:Provider"] = "fake",
                ["LlmFeedback:BaseUrl"] = "https://provider.test/anthropic",
                ["LlmFeedback:ApiKey"] = "",
                ["LlmFeedback:Model"] = "MiniMax-M2.7",
                ["LlmFeedback:TimeoutMs"] = "5000",
                ["LlmFeedback:MaxInputLength"] = "12000",
                ["LlmFeedback:MaxOutputTokens"] = "777",
                ["LlmFeedback:RateLimit:RequestsPerWindow"] = "12",
                ["LlmFeedback:RateLimit:WindowSeconds"] = "30",
                ["LlmFeedback:RedactionEnabled"] = "true",
                ["Ai:Provider"] = "Anthropic",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<LlmFeedbackOptions>>().Value;
        options.Enabled.Should().BeFalse();
        options.Provider.Should().Be("fake");
        options.BaseUrl.Should().Be("https://provider.test/anthropic");
        options.ApiKey.Should().BeEmpty();
        options.Model.Should().Be("MiniMax-M2.7");
        options.TimeoutMs.Should().Be(5000);
        options.MaxInputLength.Should().Be(12000);
        options.MaxOutputTokens.Should().Be(777);
        options.RateLimit.RequestsPerWindow.Should().Be(12);
        options.RateLimit.WindowSeconds.Should().Be(30);
        options.RedactionEnabled.Should().BeTrue();
    }

    [Fact]
    public void LlmFeedbackOptions_IgnoreAiProviderForFeedbackClientResolution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmFeedback:Provider"] = "fake",
                ["Ai:Provider"] = "Anthropic",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILlmFeedbackClient>().Should().BeOfType<FakeLlmFeedbackClient>();
    }

    [Fact]
    public void LlmFeedbackOptions_BindEnvironmentOverride()
    {
        Environment.SetEnvironmentVariable("LLM_FEEDBACK__ENABLED", "false");
        Environment.SetEnvironmentVariable("LlmFeedback__BaseUrl", "https://env.test/anthropic");
        Environment.SetEnvironmentVariable("LlmFeedback__Model", "MiniMax-M2.7");
        Environment.SetEnvironmentVariable("LlmFeedback__ApiKey", "env-provider-key");
        Environment.SetEnvironmentVariable("LlmFeedback__MaxInputLength", "999");
        Environment.SetEnvironmentVariable("LlmFeedback__MaxOutputTokens", "333");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LlmFeedback:Enabled"] = "true",
                    ["LlmFeedback:Provider"] = "fake",
                    ["LlmFeedback:Model"] = "fake-local-v1",
                    ["LlmFeedback:TimeoutMs"] = "5000",
                })
                .AddEnvironmentVariables()
                .Build();
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddInfrastructure(configuration);

            var options = services.BuildServiceProvider().GetRequiredService<IOptions<LlmFeedbackOptions>>().Value;
            options.Enabled.Should().BeFalse();
            options.BaseUrl.Should().Be("https://env.test/anthropic");
            options.Model.Should().Be("MiniMax-M2.7");
            options.ApiKey.Should().Be("env-provider-key");
            options.MaxInputLength.Should().Be(999);
            options.MaxOutputTokens.Should().Be(333);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LLM_FEEDBACK__ENABLED", null);
            Environment.SetEnvironmentVariable("LlmFeedback__BaseUrl", null);
            Environment.SetEnvironmentVariable("LlmFeedback__Model", null);
            Environment.SetEnvironmentVariable("LlmFeedback__ApiKey", null);
            Environment.SetEnvironmentVariable("LlmFeedback__MaxInputLength", null);
            Environment.SetEnvironmentVariable("LlmFeedback__MaxOutputTokens", null);
        }
    }

    [Theory]
    [InlineData("fake", typeof(FakeLlmFeedbackClient))]
    [InlineData("minimax", typeof(MinimaxLlmFeedbackClient))]
    public void RegisterLlmFeedbackClient_SelectsConfiguredProvider(string providerName, Type expectedType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmFeedback:Enabled"] = "true",
                ["LlmFeedback:Provider"] = providerName,
                ["LlmFeedback:BaseUrl"] = "https://provider.test/anthropic",
                ["LlmFeedback:ApiKey"] = providerName == "minimax" ? "test-provider-key" : "",
                ["LlmFeedback:Model"] = "MiniMax-M2.7",
                ["LlmFeedback:TimeoutMs"] = "5000",
                ["LlmFeedback:MaxInputLength"] = "32000",
                ["LlmFeedback:MaxOutputTokens"] = "1024",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        services.BuildServiceProvider().GetRequiredService<ILlmFeedbackClient>().Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("anthropic")]
    public void RegisterLlmFeedbackClient_RejectsInvalidProvider(string providerName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LlmFeedback:Provider"] = providerName })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        var action = () => services.AddInfrastructure(configuration);

        action.Should().Throw<InvalidOperationException>().WithMessage("*fake, minimax*");
    }

    [Fact]
    public void RegisterLlmFeedbackClient_EnabledMinimaxWithoutApiKeyFailsFastWithoutLeakingAValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmFeedback:Enabled"] = "true",
                ["LlmFeedback:Provider"] = "minimax",
                ["LlmFeedback:BaseUrl"] = "https://provider.test/anthropic",
                ["LlmFeedback:ApiKey"] = "",
                ["LlmFeedback:Model"] = "MiniMax-M2.7",
                ["LlmFeedback:TimeoutMs"] = "5000",
                ["LlmFeedback:MaxInputLength"] = "32000",
                ["LlmFeedback:MaxOutputTokens"] = "1024",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        var action = () => services.AddInfrastructure(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*server-side API key*")
            .Which.Message.Should().NotContain("ApiKey=");
    }
}
