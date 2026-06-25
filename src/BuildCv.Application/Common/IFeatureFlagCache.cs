namespace BuildCv.Application.Common;

public interface IFeatureFlagCache
{
    void Invalidate(string name);
}
