namespace BuildCv.Application.Features.Payments;

public sealed record CheckoutSession
{
    public string SessionId { get; init; } = "";
    public string PublicKey { get; init; } = "";
    public long AmountInCents { get; init; }
    public string Currency { get; init; } = "COP";
    public string Reference { get; init; } = "";
}
