namespace BuildCv.Domain.Adapt;

public sealed class SeverityPolicy
{
    public Severity Classify(IReadOnlyList<EntityInvention> inventions)
    {
        if (inventions.Count == 0)
        {
            return Severity.None;
        }

        var hardCount = inventions.Count(i => i.InventionSeverity == InventionSeverity.Hard);
        var softCount = inventions.Count(i => i.InventionSeverity == InventionSeverity.Soft);

        if (hardCount >= 1)
        {
            return Severity.Critical;
        }

        return softCount >= 3 ? Severity.Critical : Severity.Warning;
    }
}
