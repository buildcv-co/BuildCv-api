namespace BuildCv.Infrastructure.Iterations;

public interface IIterationCleanupCapable
{
    Task<int> DeleteExpiredAsync(DateTime olderThan, CancellationToken ct = default);
}
