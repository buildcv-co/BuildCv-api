namespace BuildCv.Application.Common;

public interface ICreditsFeatureFlag
{
    bool IsEnabled { get; }
}
