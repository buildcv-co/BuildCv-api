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
            .AddJsonFile(Path.Combine(apiProject, "appsettings.Development.json"))
            .Build();
        var services = new ServiceCollection();

        configuration.GetSection("LlmFeedback").Exists().Should().BeTrue();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<LlmFeedbackOptions>>().Value;
        options.Enabled.Should().BeFalse();
        options.Provider.Should().Be("fake");
        options.Model.Should().Be("fake-local-v1");
        options.TimeoutMs.Should().Be(5000);
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
                ["LlmFeedback:Model"] = "fake-local-v1",
                ["LlmFeedback:TimeoutMs"] = "5000",
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
        options.Model.Should().Be("fake-local-v1");
        options.TimeoutMs.Should().Be(5000);
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
        }
        finally
        {
            Environment.SetEnvironmentVariable("LLM_FEEDBACK__ENABLED", null);
        }
    }
}
