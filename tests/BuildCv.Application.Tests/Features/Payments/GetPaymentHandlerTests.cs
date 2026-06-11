using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Payments;

public sealed class GetPaymentHandlerTests
{
    private readonly TestPaymentStore _store = new();
    private readonly GetPaymentHandler _handler;

    public GetPaymentHandlerTests()
    {
        _handler = new GetPaymentHandler(_store);
    }

    [Fact]
    public async Task HandleAsync_returns_payment_when_exists()
    {
        var payment = CreatePayment();
        await _store.AddAsync(payment);

        var result = await _handler.HandleAsync(
            new GetPaymentQuery { PaymentId = payment.Id, UserId = payment.UserId.ToString() },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task HandleAsync_returns_failure_when_not_found()
    {
        var result = await _handler.HandleAsync(
            new GetPaymentQuery { PaymentId = Guid.NewGuid(), UserId = Guid.NewGuid().ToString() },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PAYMENT/NOT_FOUND");
    }

    [Fact]
    public async Task HandleAsync_rejects_access_by_different_user()
    {
        var payment = CreatePayment();
        await _store.AddAsync(payment);

        var result = await _handler.HandleAsync(
            new GetPaymentQuery { PaymentId = payment.Id, UserId = Guid.NewGuid().ToString() },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PAYMENT/NOT_FOUND");
    }

    private static Payment CreatePayment() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        PackageId = "starter",
        Credits = 10,
        AmountInCents = 1_500_000,
        Currency = "COP",
        Status = PaymentStatus.Pending,
        IdempotencyKey = $"idem-{Guid.NewGuid():N}",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
