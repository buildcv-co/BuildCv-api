using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Iterations;

public sealed class IterationPortContractsTests
{
    [Fact]
    public async Task IIterationStore_SaveRequest_then_GetById_returns_same_request()
    {
        var store = new TestIterationStore();
        var request = IterationRequest.Create(Guid.NewGuid(), "cv", "vacancy", 5, 50, DateTime.UtcNow);

        await store.SaveRequestAsync(request);
        var (loadedRequest, _) = await store.GetByIdAsync(request.RequestId);

        loadedRequest.Should().NotBeNull();
        loadedRequest!.RequestId.Should().Be(request.RequestId);
        loadedRequest.Status.Should().Be(RequestStatus.Running);
    }

    [Fact]
    public async Task IIterationStore_SaveResult_then_GetById_returns_same_result()
    {
        var store = new TestIterationStore();
        var requestId = Guid.NewGuid();
        var result = new IterationResult
        {
            RequestId = requestId,
            Status = RequestStatus.Completed,
            CreditsConsumed = 5,
            CompletedAt = DateTime.UtcNow,
        };

        await store.SaveResultAsync(result);
        var (_, loadedResult) = await store.GetByIdAsync(requestId);

        loadedResult.Should().NotBeNull();
        loadedResult!.RequestId.Should().Be(requestId);
        loadedResult.Status.Should().Be(RequestStatus.Completed);
        loadedResult.CreditsConsumed.Should().Be(5);
    }
}
