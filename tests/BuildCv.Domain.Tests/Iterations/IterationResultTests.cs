using BuildCv.Domain.Iterations;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Iterations;

public sealed class IterationResultTests
{
    [Fact]
    public void Result_defaults_to_running_status_empty_steps_and_24h_expiry()
    {
        var before = DateTime.UtcNow;
        var result = new IterationResult { RequestId = Guid.NewGuid() };
        var after = DateTime.UtcNow.AddSeconds(1);

        result.Status.Should().Be(RequestStatus.Running);
        result.BestStep.Should().BeNull();
        result.AllSteps.Should().BeEmpty();
        result.ProbabilityWarning.Should().BeNull();
        result.CreditsConsumed.Should().Be(0);
        result.CompletedAt.Should().BeOnOrAfter(before).And.BeBefore(after);
        result.ExpiresAt.Should().BeOnOrAfter(before.AddDays(1)).And.BeBefore(after.AddDays(1));
    }
}
