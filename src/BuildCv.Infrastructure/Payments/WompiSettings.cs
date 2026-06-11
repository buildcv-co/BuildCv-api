namespace BuildCv.Infrastructure.Payments;

public sealed class WompiSettings
{
    public const string SectionName = "Wompi";

    public bool Enabled { get; init; }
    public string Environment { get; init; } = "sandbox";
    public string PublicKey { get; init; } = "";
    public string PrivateKey { get; init; } = "";
    public string WebhookSecret { get; init; } = "";

    public string BaseUrl => Environment.Equals("production", StringComparison.OrdinalIgnoreCase)
        ? "https://api.wompi.co"
        : "https://api.wompi.sandbox";
}
