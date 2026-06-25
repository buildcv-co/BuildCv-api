using BuildCv.Domain.Auth;
using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Iterations;
using BuildCv.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Tests.Iterations;

public sealed class EfIterationStoreTests : IDisposable
{
    private readonly BuildCvDbContext _dbContext;
    private readonly EfIterationStore _store;
    private readonly Guid _userId;

    public EfIterationStoreTests()
    {
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BuildCvDbContext(options);
        _dbContext.Users.Add(new User { Id = Guid.NewGuid(), Email = "u@example.com", Name = "U", Provider = "google", ProviderId = "g-1" });
        _dbContext.SaveChanges();
        _userId = _dbContext.Users.Local.First().Id;
        _store = new EfIterationStore(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task SaveRequestAsync_persists_request_with_status_running()
    {
        var request = NewRequest(RequestStatus.Running);

        await _store.SaveRequestAsync(request);

        var persisted = await _dbContext.IterationRequests.FindAsync(request.RequestId);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(_userId);
        persisted.CvText.Should().Be(request.CvText);
        persisted.Status.Should().Be(RequestStatus.Running);
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_persists_new_status()
    {
        var request = NewRequest(RequestStatus.Running);
        await _store.SaveRequestAsync(request);

        await _store.UpdateRequestStatusAsync(request.RequestId, RequestStatus.Completed);

        var persisted = await _dbContext.IterationRequests.FindAsync(request.RequestId);
        persisted!.Status.Should().Be(RequestStatus.Completed);
    }

    [Fact]
    public async Task SaveResultAsync_persists_result_with_default_expires_at_24h()
    {
        var request = NewRequest(RequestStatus.Running);
        await _store.SaveRequestAsync(request);
        var before = DateTime.UtcNow;
        var result = NewResult(request.RequestId, RequestStatus.Completed);

        await _store.SaveResultAsync(result);
        var after = DateTime.UtcNow;

        var persisted = await _dbContext.IterationResults.FindAsync(request.RequestId);
        persisted.Should().NotBeNull();
        persisted!.CreditsConsumed.Should().Be(5);
        persisted.ExpiresAt.Should().BeOnOrAfter(before.AddHours(24).AddSeconds(-1));
        persisted.ExpiresAt.Should().BeOnOrBefore(after.AddHours(24).AddSeconds(1));
    }

    [Fact]
    public async Task SaveResultAsync_round_trips_best_step_and_all_steps_as_jsonb()
    {
        var request = NewRequest(RequestStatus.Running);
        await _store.SaveRequestAsync(request);

        var step = new IterationStep
        {
            IterationNumber = 1,
            AdaptedCvText = "adapted cv",
            Score = 78,
            PassedArtI = true,
            Duration = TimeSpan.FromMilliseconds(500),
            Timestamp = DateTime.UtcNow,
        };
        var result = new IterationResult
        {
            RequestId = request.RequestId,
            Status = RequestStatus.Completed,
            BestStep = step,
            AllSteps = new[] { step },
            ProbabilityWarning = "warning text",
            CreditsConsumed = 5,
            CompletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        };

        await _store.SaveResultAsync(result);

        var (fetchedReq, fetchedRes) = await _store.GetByIdAsync(request.RequestId);
        fetchedRes.Should().NotBeNull();
        fetchedRes!.BestStep.Should().NotBeNull();
        fetchedRes.BestStep!.IterationNumber.Should().Be(1);
        fetchedRes.BestStep.Score.Should().Be(78);
        fetchedRes.BestStep.AdaptedCvText.Should().Be("adapted cv");
        fetchedRes.AllSteps.Should().HaveCount(1);
        fetchedRes.AllSteps[0].PassedArtI.Should().BeTrue();
        fetchedRes.ProbabilityWarning.Should().Be("warning text");
    }

    [Fact]
    public async Task SaveResultAsync_persists_null_best_step()
    {
        var request = NewRequest(RequestStatus.Running);
        await _store.SaveRequestAsync(request);
        var result = new IterationResult
        {
            RequestId = request.RequestId,
            Status = RequestStatus.Failed,
            BestStep = null,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 5,
            CompletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        };

        await _store.SaveResultAsync(result);

        var (fetchedReq, fetchedRes) = await _store.GetByIdAsync(request.RequestId);
        fetchedRes.Should().NotBeNull();
        fetchedRes!.BestStep.Should().BeNull();
        fetchedRes.Status.Should().Be(RequestStatus.Failed);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_request_missing()
    {
        var (fetchedReq, fetchedRes) = await _store.GetByIdAsync(Guid.NewGuid());

        fetchedReq.Should().BeNull();
        fetchedRes.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_returns_only_request_when_result_not_saved()
    {
        var request = NewRequest(RequestStatus.Running);
        await _store.SaveRequestAsync(request);

        var (fetchedReq, fetchedRes) = await _store.GetByIdAsync(request.RequestId);

        fetchedReq.Should().NotBeNull();
        fetchedReq!.RequestId.Should().Be(request.RequestId);
        fetchedRes.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExpiredAsync_removes_rows_older_than_threshold()
    {
        var oldReq = NewRequest(RequestStatus.Completed);
        await _store.SaveRequestAsync(oldReq);
        var oldResult = new IterationResult
        {
            RequestId = oldReq.RequestId,
            Status = RequestStatus.Completed,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 5,
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        };
        await _store.SaveResultAsync(oldResult);

        var freshReq = NewRequest(RequestStatus.Completed);
        await _store.SaveRequestAsync(freshReq);
        var freshResult = new IterationResult
        {
            RequestId = freshReq.RequestId,
            Status = RequestStatus.Completed,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 5,
            CompletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(23),
        };
        await _store.SaveResultAsync(freshResult);

        var deleted = await _store.DeleteExpiredAsync(DateTime.UtcNow);

        deleted.Should().Be(1);
        var (oldReq2, oldRes2) = await _store.GetByIdAsync(oldReq.RequestId);
        oldRes2.Should().BeNull();
        var (freshReq2, freshRes2) = await _store.GetByIdAsync(freshReq.RequestId);
        freshRes2.Should().NotBeNull();
    }

    [Fact]
    public void EfIterationStore_implements_contract()
    {
        _store.Should().BeAssignableTo<BuildCv.Application.Features.Iterations.IIterationStore>();
    }

    private IterationRequest NewRequest(RequestStatus status)
    {
        var now = DateTime.UtcNow;
        return new IterationRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = _userId,
            CvText = "cv text",
            VacancyText = "vacancy text",
            IterationCount = 5,
            ProbabilityThreshold = 50,
            CreatedAt = now,
            Status = status,
        };
    }

    private static IterationResult NewResult(Guid requestId, RequestStatus status) => new()
    {
        RequestId = requestId,
        Status = status,
        AllSteps = Array.Empty<IterationStep>(),
        CreditsConsumed = 5,
        CompletedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(24),
    };
}
