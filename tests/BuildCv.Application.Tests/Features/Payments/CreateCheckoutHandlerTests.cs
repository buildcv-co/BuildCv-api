using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Payments;

public sealed class CreateCheckoutHandlerTests
{
    private readonly TestPaymentStore _store = new();
    private readonly TestPaymentProvider _provider = new();
    private readonly CreateCheckoutHandler _handler;

    public CreateCheckoutHandlerTests()
    {
        _handler = new CreateCheckoutHandler(_store, _provider);
    }

    [Fact]
    public async Task HandleAsync_creates_checkout_for_valid_package()
    {
        var command = new CreateCheckoutCommand
        {
            UserId = Guid.NewGuid().ToString(),
            PackageId = "starter"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AmountInCents.Should().Be(1_500_000);
        result.Value.Currency.Should().Be("COP");
    }

    [Fact]
    public async Task HandleAsync_returns_existing_session_for_idempotent_duplicate()
    {
        var command = new CreateCheckoutCommand
        {
            UserId = Guid.NewGuid().ToString(),
            PackageId = "pro"
        };

        var first = await _handler.HandleAsync(command, CancellationToken.None);
        var second = await _handler.HandleAsync(command, CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.SessionId.Should().Be(first.Value.SessionId);
    }

    [Fact]
    public async Task HandleAsync_returns_failure_for_invalid_package()
    {
        var command = new CreateCheckoutCommand
        {
            UserId = Guid.NewGuid().ToString(),
            PackageId = "nonexistent"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PAYMENT/INVALID_PACKAGE");
    }

    [Fact]
    public async Task HandleAsync_stores_payment_record()
    {
        var command = new CreateCheckoutCommand
        {
            UserId = Guid.NewGuid().ToString(),
            PackageId = "standard"
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        var stored = await _store.GetByIdempotencyKeyAsync(result.Value.Reference);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(PaymentStatus.Pending);
        stored.Credits.Should().Be(50);
    }
}
