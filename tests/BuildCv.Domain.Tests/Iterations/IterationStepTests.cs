using BuildCv.Domain.Iterations;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Iterations;

public sealed class IterationStepTests
{
    [Fact]
    public void Step_defaults_to_empty_cv_and_zero_score_with_utc_now_timestamp()
    {
        var before = DateTime.UtcNow;
        var step = new IterationStep { IterationNumber = 1 };
        var after = DateTime.UtcNow;

        step.IterationNumber.Should().Be(1);
        step.AdaptedCvText.Should().Be("");
        step.Score.Should().Be(0);
        step.PassedArtI.Should().BeFalse();
        step.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        step.Duration.Should().Be(TimeSpan.Zero);
    }
}
