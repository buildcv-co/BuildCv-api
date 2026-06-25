using BuildCv.Domain.FeatureFlags;

namespace BuildCv.Application.Common;

public interface IFeatureFlag
{
    Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);
    Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default);
}
