using BuildCv.Application.Features.Iterations;
using BuildCv.Domain.Iterations;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Iterations;

public sealed class GetIterationResultHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_cached_result_when_present()
    {
        var store = new TestIterationStore();
        var requestId = Guid.NewGuid();
        var cached = new IterationResult
        {
            RequestId = requestId,
            Status = RequestStatus.Completed,
            CreditsConsumed = 3,
            CompletedAt = DateTime.UtcNow,
        };
        await store.SaveResultAsync(cached);
        var handler = new GetIterationResultHandler(store);

        var loaded = await handler.HandleAsync(requestId);

        loaded.Should().NotBeNull();
        loaded!.RequestId.Should().Be(requestId);
        loaded.Status.Should().Be(RequestStatus.Completed);
        loaded.CreditsConsumed.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_not_found()
    {
        var store = new TestIterationStore();
        var handler = new GetIterationResultHandler(store);

        var loaded = await handler.HandleAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_throws_when_requestId_is_empty()
    {
        var store = new TestIterationStore();
        var handler = new GetIterationResultHandler(store);

        var act = () => handler.HandleAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*RequestId*");
    }
}
