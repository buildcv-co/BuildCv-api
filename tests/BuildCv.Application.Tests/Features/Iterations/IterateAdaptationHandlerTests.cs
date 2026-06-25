using BuildCv.Application.Features.Iterations;
using BuildCv.Application.Tests.Credits;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Iterations;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Iterations;

public sealed class IterateAdaptationHandlerTests
{
    [Fact]
    public async Task HandleAsync_runs_n_iterations_and_returns_completed_status()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2", "adapted-3");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 3, 50);

        result.Status.Should().Be(RequestStatus.Completed);
        result.AllSteps.Should().HaveCount(3);
        result.AllSteps[0].IterationNumber.Should().Be(1);
        result.AllSteps[1].IterationNumber.Should().Be(2);
        result.AllSteps[2].IterationNumber.Should().Be(3);
        result.CreditsConsumed.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_selects_step_with_highest_score_among_passing_steps()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2", "adapted-3");
        var scores = new Queue<int>(new[] { 40, 85, 60 });
        harness.Scoring.ScoreSelector = _ => scores.Dequeue();
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 3, 50);

        result.Status.Should().Be(RequestStatus.Completed);
        result.BestStep.Should().NotBeNull();
        result.BestStep!.Score.Should().Be(85);
        result.BestStep.IterationNumber.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_skips_critical_severity_steps_from_best_selection()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany(
            "Worked at RealCorp with C#",
            "Worked at BogusCorp with C#",
            "Worked at BogusCorp3 with C#");
        harness.Scoring.ScoreSelector = call => call switch
        {
            0 => 40,
            _ => 99,
        };
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "Worked at RealCorp with C#", "job text", 3, 50);

        var step1 = result.AllSteps.Single(s => s.IterationNumber == 1);
        step1.PassedArtI.Should().BeTrue();
        step1.Score.Should().Be(40);

        var step2 = result.AllSteps.Single(s => s.IterationNumber == 2);
        step2.PassedArtI.Should().BeFalse();
        step2.Score.Should().Be(0);

        var step3 = result.AllSteps.Single(s => s.IterationNumber == 3);
        step3.PassedArtI.Should().BeFalse();
        step3.Score.Should().Be(0);

        result.Status.Should().Be(RequestStatus.Completed);
        result.BestStep.Should().NotBeNull();
        result.BestStep!.IterationNumber.Should().Be(1);
        result.BestStep.Score.Should().Be(40);
    }

    [Fact]
    public async Task HandleAsync_returns_failed_status_when_all_iterations_are_critical()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany(
            "I worked at BogusCorp1 with C#",
            "I worked at BogusCorp2 with C#",
            "I worked at BogusCorp3 with C#");
        harness.Scoring.ScoreSelector = _ => 0;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "I worked at RealCorp with C#", "job text", 3, 50);

        result.Status.Should().Be(RequestStatus.Failed);
        result.BestStep.Should().BeNull();
        result.ProbabilityWarning.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_generates_probability_warning_when_best_score_below_threshold()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2");
        harness.Scoring.ScoreSelector = _ => 30;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 2, 50);

        result.Status.Should().Be(RequestStatus.Completed);
        result.ProbabilityWarning.Should().NotBeNullOrEmpty();
        result.ProbabilityWarning.Should().Contain("30");
        result.ProbabilityWarning.Should().Contain("50");
    }

    [Fact]
    public async Task HandleAsync_omits_probability_warning_when_best_score_meets_threshold()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1");
        harness.Scoring.ScoreSelector = _ => 75;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 1, 50);

        result.Status.Should().Be(RequestStatus.Completed);
        result.BestStep!.Score.Should().Be(75);
        result.ProbabilityWarning.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_debits_iteration_count_credits_before_loop()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2", "adapted-3");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 10);

        await handler.HandleAsync(userId, "my cv", "job text", 3, 50);

        var balance = await harness.Ledger.GetBalanceAsync(userId, CancellationToken.None);
        balance.Should().Be(7);
        harness.Ledger.AllEntries.Should().ContainSingle(e =>
            e.Reason == CreditLedgerReason.Consumption &&
            e.Delta == -3 &&
            e.Reference.StartsWith("iterate:"));
    }

    [Fact]
    public async Task HandleAsync_throws_insufficient_credits_when_balance_too_low()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 1);

        var act = () => handler.HandleAsync(userId, "my cv", "job text", 5, 50);

        var assertion = await act.Should().ThrowAsync<InsufficientCreditsException>();
        assertion.Which.Required.Should().Be(5);
        assertion.Which.Balance.Should().Be(1);
        harness.Ledger.AllEntries.Should().BeEmpty();
    }
}
