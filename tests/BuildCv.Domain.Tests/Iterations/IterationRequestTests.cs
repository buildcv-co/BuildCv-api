using BuildCv.Domain.Iterations;
using FluentAssertions;

namespace BuildCv.Domain.Tests.Iterations;

public sealed class IterationRequestTests
{
    [Fact]
    public void Create_with_valid_args_assigns_running_status_and_fresh_request_id()
    {
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var request = IterationRequest.Create(userId, "my cv text", "job text", 5, 50, now);

        request.UserId.Should().Be(userId);
        request.CvText.Should().Be("my cv text");
        request.VacancyText.Should().Be("job text");
        request.IterationCount.Should().Be(5);
        request.ProbabilityThreshold.Should().Be(50);
        request.CreatedAt.Should().Be(now);
        request.Status.Should().Be(RequestStatus.Running);
        request.RequestId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_throws_when_iteration_count_is_outside_one_to_twenty_range()
    {
        var actLow = () => IterationRequest.Create(Guid.NewGuid(), "cv", "job", 0, 50, DateTime.UtcNow);
        var actHigh = () => IterationRequest.Create(Guid.NewGuid(), "cv", "job", 21, 50, DateTime.UtcNow);

        actLow.Should().Throw<ArgumentException>().WithMessage("*Iteration count*");
        actHigh.Should().Throw<ArgumentException>().WithMessage("*Iteration count*");
    }

    [Fact]
    public void Create_throws_when_threshold_is_outside_zero_to_hundred_range()
    {
        var actLow = () => IterationRequest.Create(Guid.NewGuid(), "cv", "job", 5, -1, DateTime.UtcNow);
        var actHigh = () => IterationRequest.Create(Guid.NewGuid(), "cv", "job", 5, 101, DateTime.UtcNow);

        actLow.Should().Throw<ArgumentException>().WithMessage("*Threshold*");
        actHigh.Should().Throw<ArgumentException>().WithMessage("*Threshold*");
    }
}
