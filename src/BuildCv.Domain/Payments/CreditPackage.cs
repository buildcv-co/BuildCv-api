namespace BuildCv.Domain.Payments;

public sealed record CreditPackage(string Id, int Credits, long PriceInCents, string Currency = "COP")
{
    public static readonly CreditPackage Starter = new("starter", 10, 1_500_000);
    public static readonly CreditPackage Standard = new("standard", 50, 6_000_000);
    public static readonly CreditPackage Pro = new("pro", 100, 10_000_000);

    public static readonly IReadOnlyList<CreditPackage> All = [Starter, Standard, Pro];

    public static CreditPackage? FindById(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
