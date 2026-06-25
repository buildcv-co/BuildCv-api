using BuildCv.Application.Common;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Credits;

public sealed class CreditsFeatureFlag(IOptions<CreditsOptions> options) : ICreditsFeatureFlag
{
    public bool IsEnabled => options.Value.Enabled;
}
