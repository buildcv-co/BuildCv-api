namespace BuildCv.Domain.Invoicing;

public sealed record NumberingRange
{
    public Guid Id { get; init; }
    public int ProviderId { get; init; }
    public string Prefix { get; init; } = "";
    public int From { get; init; }
    public int To { get; init; }
    public int Current { get; init; }
    public string Status { get; init; } = "Active";
    public DateTime CreatedAt { get; init; }
}
