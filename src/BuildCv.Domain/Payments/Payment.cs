namespace BuildCv.Domain.Payments;

public sealed record Payment
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string PackageId { get; init; } = "";
    public int Credits { get; init; }
    public long AmountInCents { get; init; }
    public string Currency { get; init; } = "COP";
    public PaymentStatus Status { get; init; }
    public string? WompiTransactionId { get; init; }
    public string? WompiPaymentLink { get; init; }
    public string IdempotencyKey { get; init; } = "";
    public string? ProviderSessionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
}
