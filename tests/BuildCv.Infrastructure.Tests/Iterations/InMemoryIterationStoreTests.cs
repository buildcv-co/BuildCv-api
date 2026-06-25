using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;
using BuildCv.Infrastructure.Iterations;
using FluentAssertions;

namespace BuildCv.Infrastructure.Tests.Iterations;

public sealed class InMemoryIterationStoreTests
{
    private readonly InMemoryIterationStore _store = new();

    [Fact]
    public async Task SaveRequestAsync_then_GetByIdAsync_returns_request()
    {
        var request = NewRequest(RequestStatus.Running);

        await _store.SaveRequestAsync(request);
        var (fetchedReq, fetchedRes) = await _store.GetByIdAsync(request.RequestId);

        fetchedReq.Should().NotBeNull();
        fetchedReq!.RequestId.Should().Be(request.RequestId);
        fetchedRes.Should().BeNull();
    }

    [Fact]
    public async Task SaveResultAsync_then_GetByIdAsync_returns_result()
    {
        var request = NewRequest(RequestStatus.Running);
        await _store.SaveRequestAsync(request);
        var result = NewResult(request.RequestId, RequestStatus.Completed);

        await _store.SaveResultAsync(result);
        var (_, fetchedRes) = await _store.GetByIdAsync(request.RequestId);

        fetchedRes.Should().NotBeNull();
        fetchedRes!.RequestId.Should().Be(request.RequestId);
        fetchedRes.Status.Should().Be(RequestStatus.Completed);
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_replaces_status()
    {
        var request = NewRequest(RequestStatus.Running);
        await _store.SaveRequestAsync(request);

        await _store.UpdateRequestStatusAsync(request.RequestId, RequestStatus.Failed);
        var (fetchedReq, _) = await _store.GetByIdAsync(request.RequestId);

        fetchedReq!.Status.Should().Be(RequestStatus.Failed);
    }

    [Fact]
    public async Task UpdateRequestStatusAsync_on_missing_request_is_no_op()
    {
        var act = async () => await _store.UpdateRequestStatusAsync(Guid.NewGuid(), RequestStatus.Completed);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_missing()
    {
        var (fetchedReq, fetchedRes) = await _store.GetByIdAsync(Guid.NewGuid());

        fetchedReq.Should().BeNull();
        fetchedRes.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExpiredAsync_removes_results_with_expires_at_in_past()
    {
        var request = NewRequest(RequestStatus.Completed);
        await _store.SaveRequestAsync(request);
        var oldResult = new IterationResult
        {
            RequestId = request.RequestId,
            Status = RequestStatus.Completed,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 5,
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        };
        await _store.SaveResultAsync(oldResult);

        var deleted = await _store.DeleteExpiredAsync(DateTime.UtcNow);

        deleted.Should().Be(1);
        var (_, res) = await _store.GetByIdAsync(request.RequestId);
        res.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExpiredAsync_keeps_fresh_results()
    {
        var request = NewRequest(RequestStatus.Completed);
        await _store.SaveRequestAsync(request);
        var freshResult = new IterationResult
        {
            RequestId = request.RequestId,
            Status = RequestStatus.Completed,
            AllSteps = Array.Empty<IterationStep>(),
            CreditsConsumed = 5,
            CompletedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(23),
        };
        await _store.SaveResultAsync(freshResult);

        var deleted = await _store.DeleteExpiredAsync(DateTime.UtcNow);

        deleted.Should().Be(0);
        var (_, res) = await _store.GetByIdAsync(request.RequestId);
        res.Should().NotBeNull();
    }

    [Fact]
    public void InMemoryIterationStore_implements_contract()
    {
        _store.Should().BeAssignableTo<IIterationStore>();
    }

    private static IterationRequest NewRequest(RequestStatus status) => new()
    {
        RequestId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CvText = "cv text",
        VacancyText = "vacancy text",
        IterationCount = 5,
        ProbabilityThreshold = 50,
        CreatedAt = DateTime.UtcNow,
        Status = status,
    };

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
