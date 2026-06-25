namespace BuildCv.Application.Features.Subscriptions;

public interface ISubscriptionFeatureFlag
{
    bool IsEnabled { get; }
}
