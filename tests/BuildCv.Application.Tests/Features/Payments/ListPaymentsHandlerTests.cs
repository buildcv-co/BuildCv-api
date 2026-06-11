using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using FluentAssertions;

namespace BuildCv.Application.Tests.Features.Payments;

public sealed class ListPaymentsHandlerTests
{
    private readonly TestPaymentStore _store = new();
    private readonly ListPaymentsHandler _handler;

    public ListPaymentsHandlerTests()
    {
        _handler = new ListPaymentsHandler(_store);
    }

    [Fact]
    public async Task HandleAsync_returns_payments_for_user()
    {
        var userId = Guid.NewGuid();
        await _store.AddAsync(CreatePayment(userId, "starter"));
        await _store.AddAsync(CreatePayment(userId, "pro"));
        await _store.AddAsync(CreatePayment(Guid.NewGuid(), "starter"));

        var result = await _handler.HandleAsync(
            new ListPaymentsQuery { UserId = userId.ToString(), Page = 1, PerPage = 10 },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_returns_empty_list_when_no_payments()
    {
        var result = await _handler.HandleAsync(
            new ListPaymentsQuery { UserId = Guid.NewGuid().ToString(), Page = 1, PerPage = 10 },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_respects_pagination()
    {
        var userId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await _store.AddAsync(CreatePayment(userId, "starter"));
        }

        var page1 = await _handler.HandleAsync(
            new ListPaymentsQuery { UserId = userId.ToString(), Page = 1, PerPage = 2 },
            CancellationToken.None);
        var page2 = await _handler.HandleAsync(
            new ListPaymentsQuery { UserId = userId.ToString(), Page = 2, PerPage = 2 },
            CancellationToken.None);

        page1.Value.Should().HaveCount(2);
        page2.Value.Should().HaveCount(2);
    }

    private static Payment CreatePayment(Guid userId, string packageId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PackageId = packageId,
        Credits = CreditPackage.FindById(packageId)!.Credits,
        AmountInCents = CreditPackage.FindById(packageId)!.PriceInCents,
        Currency = "COP",
        Status = PaymentStatus.Pending,
        IdempotencyKey = $"idem-{Guid.NewGuid():N}",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
