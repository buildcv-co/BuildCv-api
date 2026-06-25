using BuildCv.Application.Common;
using BuildCv.Infrastructure.Credits;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.Credits;

public sealed class CreditsFeatureFlagTests
{
    [Fact]
    public void IsEnabled_returns_true_when_config_is_true()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Credits:Enabled"] = "true",
            })
            .Build();
        services.Configure<CreditsOptions>(configuration.GetSection(CreditsOptions.SectionName));
        services.AddSingleton<ICreditsFeatureFlag, CreditsFeatureFlag>();

        var flag = services.BuildServiceProvider().GetRequiredService<ICreditsFeatureFlag>();

        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_returns_false_when_config_is_false()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Credits:Enabled"] = "false",
            })
            .Build();
        services.Configure<CreditsOptions>(configuration.GetSection(CreditsOptions.SectionName));
        services.AddSingleton<ICreditsFeatureFlag, CreditsFeatureFlag>();

        var flag = services.BuildServiceProvider().GetRequiredService<ICreditsFeatureFlag>();

        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_returns_false_when_config_missing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        services.Configure<CreditsOptions>(configuration.GetSection(CreditsOptions.SectionName));
        services.AddSingleton<ICreditsFeatureFlag, CreditsFeatureFlag>();

        var flag = services.BuildServiceProvider().GetRequiredService<ICreditsFeatureFlag>();

        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CreditsOptions_binds_section_name_correctly()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Credits:Enabled"] = "true",
            })
            .Build();
        services.Configure<CreditsOptions>(configuration.GetSection(CreditsOptions.SectionName));

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<CreditsOptions>>().Value;

        options.Enabled.Should().BeTrue();
    }
}
