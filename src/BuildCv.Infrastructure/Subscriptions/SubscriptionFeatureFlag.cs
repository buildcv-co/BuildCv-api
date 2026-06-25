using BuildCv.Application.Features.Subscriptions;
using Microsoft.Extensions.Configuration;

namespace BuildCv.Infrastructure.Subscriptions;

public sealed class SubscriptionFeatureFlag(IConfiguration configuration) : ISubscriptionFeatureFlag
{
    public const string SectionName = "SubscriptionRecurring";

    public bool IsEnabled => configuration.GetValue<bool>($"{SectionName}:Enabled", false);
}
