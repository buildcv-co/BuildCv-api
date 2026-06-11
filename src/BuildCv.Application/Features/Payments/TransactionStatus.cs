namespace BuildCv.Application.Features.Payments;

public sealed record TransactionStatus
{
    public string WompiTransactionId { get; init; } = "";
    public string Status { get; init; } = "";
    public long AmountInCents { get; init; }
}
