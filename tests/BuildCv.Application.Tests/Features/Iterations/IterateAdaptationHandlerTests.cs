using BuildCv.Application.Features.Adapt;
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

    [Fact]
    public async Task HandleAsync_passes_seed_in_format_requestId_colon_iteration_number_per_iteration()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2", "adapted-3");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 3, 50);

        harness.Ai.Calls.Should().HaveCount(3);
        harness.Ai.Calls[0].Should().Contain($"{result.RequestId}:1");
        harness.Ai.Calls[1].Should().Contain($"{result.RequestId}:2");
        harness.Ai.Calls[2].Should().Contain($"{result.RequestId}:3");
    }

    [Fact]
    public async Task HandleAsync_passes_different_seeds_for_each_iteration_within_same_request()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        await handler.HandleAsync(userId, "my cv", "job text", 2, 50);

        var seed1 = ExtractSeed(harness.Ai.Calls[0]);
        var seed2 = ExtractSeed(harness.Ai.Calls[1]);
        seed1.Should().NotBe(seed2);
        seed1.Should().EndWith(":1");
        seed2.Should().EndWith(":2");
    }

    [Fact]
    public async Task HandleAsync_two_requests_with_different_request_ids_produce_different_seed_prefixes()
    {
        var harness1 = new IterationHandlerHarness();
        harness1.Ai.EnqueueMany("adapted-1");
        harness1.Scoring.ScoreSelector = _ => 60;
        var handler1 = harness1.BuildHandler();
        var userId1 = Guid.NewGuid();
        harness1.Ledger.SeedBalance(userId1, 5);

        await handler1.HandleAsync(userId1, "my cv", "job text", 1, 50);

        var harness2 = new IterationHandlerHarness();
        harness2.Ai.EnqueueMany("adapted-1");
        harness2.Scoring.ScoreSelector = _ => 60;
        var handler2 = harness2.BuildHandler();
        var userId2 = Guid.NewGuid();
        harness2.Ledger.SeedBalance(userId2, 5);

        await handler2.HandleAsync(userId2, "my cv", "job text", 1, 50);

        ExtractSeed(harness1.Ai.Calls[0]).Should().NotBe(ExtractSeed(harness2.Ai.Calls[0]));
    }

    [Fact]
    public void PromptBuilder_omits_seed_block_when_iteration_seed_is_null()
    {
        var prompt = new PromptBuilder().Build("my cv", "job text", iterationSeed: null);

        prompt.Should().NotContain("IterationSeed:");
    }

    [Fact]
    public void PromptBuilder_includes_seed_block_when_iteration_seed_is_supplied()
    {
        var prompt = new PromptBuilder().Build("my cv", "job text", iterationSeed: "abc:7");

        prompt.Should().Contain("IterationSeed: abc:7");
    }

    [Fact]
    public async Task HandleAsync_per_iteration_timeout_records_failed_step_and_continues_to_next_iteration()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.Enqueue("adapted-1");
        harness.Ai.ThrowCancellationOnCall(1);
        harness.Ai.Enqueue("adapted-3");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 3, 50);

        result.AllSteps.Should().HaveCount(3);
        result.AllSteps[0].PassedArtI.Should().BeTrue();
        result.AllSteps[1].PassedArtI.Should().BeFalse();
        result.AllSteps[1].Score.Should().Be(0);
        result.AllSteps[1].AdaptedCvText.Should().BeEmpty();
        result.AllSteps[2].PassedArtI.Should().BeTrue();
        result.Status.Should().Be(RequestStatus.Completed);
        result.BestStep!.IterationNumber.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_total_timeout_short_break_returns_status_timed_out_with_partial_true_when_best_exists()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2", "adapted-3", "adapted-4", "adapted-5");
        harness.Ai.PerCallDelay = TimeSpan.FromMilliseconds(80);
        harness.Scoring.ScoreSelector = _ => 60;
        harness.TotalTimeoutOverride = TimeSpan.FromMilliseconds(150);
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 10);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 5, 50);

        result.Status.Should().Be(RequestStatus.TimedOut);
        result.Partial.Should().BeTrue();
        result.BestStep.Should().NotBeNull();
        result.AllSteps.Count.Should().BeLessThan(5);
        result.CreditsConsumed.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_total_timeout_returns_status_failed_when_no_iteration_completed()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.ThrowCancellationOnCall(0);
        harness.Ai.ThrowCancellationOnCall(1);
        harness.Ai.ThrowCancellationOnCall(2);
        harness.Ai.PerCallDelay = TimeSpan.FromMilliseconds(80);
        harness.TotalTimeoutOverride = TimeSpan.FromMilliseconds(60);
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 10);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 3, 50);

        result.Status.Should().Be(RequestStatus.Failed);
        result.BestStep.Should().BeNull();
        result.Partial.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_completed_status_has_partial_false_when_no_timeout_occurred()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 5);

        var result = await handler.HandleAsync(userId, "my cv", "job text", 2, 50);

        result.Status.Should().Be(RequestStatus.Completed);
        result.Partial.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_get_by_request_id_returns_cached_result_without_calling_handler_again()
    {
        var harness = new IterationHandlerHarness();
        harness.Ai.EnqueueMany("adapted-1", "adapted-2");
        harness.Scoring.ScoreSelector = _ => 60;
        var handler = harness.BuildHandler();
        var getHandler = new GetIterationResultHandler(harness.Store);
        var userId = Guid.NewGuid();
        harness.Ledger.SeedBalance(userId, 10);

        var first = await handler.HandleAsync(userId, "my cv", "job text", 2, 50);
        var balanceAfterFirst = await harness.Ledger.GetBalanceAsync(userId, CancellationToken.None);

        var cached = await getHandler.HandleAsync(first.RequestId);
        var balanceAfterGet = await harness.Ledger.GetBalanceAsync(userId, CancellationToken.None);

        cached.Should().NotBeNull();
        cached!.RequestId.Should().Be(first.RequestId);
        cached.Status.Should().Be(first.Status);
        cached.BestStep!.IterationNumber.Should().Be(first.BestStep!.IterationNumber);
        balanceAfterGet.Should().Be(balanceAfterFirst);
        harness.Ledger.AllEntries.Count(e => e.Reason == CreditLedgerReason.Consumption).Should().Be(1);
    }

    private static string ExtractSeed(string prompt)
    {
        const string marker = "IterationSeed: ";
        var idx = prompt.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            throw new InvalidOperationException($"Prompt missing '{marker}' marker. Prompt: {prompt}");
        }
        var start = idx + marker.Length;
        var end = prompt.IndexOfAny(new[] { '\r', '\n' }, start);
        return end < 0 ? prompt[start..] : prompt[start..end];
    }
}
