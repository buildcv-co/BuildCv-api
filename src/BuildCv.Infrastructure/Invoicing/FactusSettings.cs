namespace BuildCv.Infrastructure.Invoicing;

public sealed class FactusSettings
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
