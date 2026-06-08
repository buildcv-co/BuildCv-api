namespace BuildCv.Domain.Adapt;

public sealed class CrossEntityValidator
{
    private static readonly HashSet<InventionType> HardTypes = new()
    {
        InventionType.Company,
        InventionType.Certification,
        InventionType.Date,
        InventionType.Title
    };

    public ValidationReport Validate(
        IEnumerable<string> originalEntities,
        IEnumerable<string> adaptedEntities,
        IDictionary<string, InventionType> entityTypes)
    {
        var originalSet = new HashSet<string>(originalEntities, StringComparer.OrdinalIgnoreCase);
        var adaptedSet = new HashSet<string>(adaptedEntities, StringComparer.OrdinalIgnoreCase);
        var inventions = new List<EntityInvention>();
        var warnings = new List<string>();

        var position = 0;
        foreach (var adaptedEntity in adaptedSet)
        {
            if (originalSet.Contains(adaptedEntity))
            {
                continue;
            }

            entityTypes.TryGetValue(adaptedEntity, out var type);
            var severity = HardTypes.Contains(type) ? InventionSeverity.Hard : InventionSeverity.Soft;
            inventions.Add(new EntityInvention(type, adaptedEntity, null, severity, position++));
        }

        var isValid = !inventions.Any();
        var computedSeverity = ComputeSeverity(inventions, warnings);

        return new ValidationReport(isValid, computedSeverity, inventions, warnings);
    }

    private static Severity ComputeSeverity(IReadOnlyList<EntityInvention> inventions, List<string> warnings)
    {
        if (inventions.Count == 0)
        {
            return Severity.None;
        }

        var hardCount = inventions.Count(i => i.InventionSeverity == InventionSeverity.Hard);
        var softCount = inventions.Count(i => i.InventionSeverity == InventionSeverity.Soft);

        if (hardCount >= 1 || softCount >= 3)
        {
            warnings.Add($"Invenciones detectadas: {hardCount} hard, {softCount} soft");
            return Severity.Critical;
        }

        warnings.Add($"Invenciones leves: {softCount}");
        return Severity.Warning;
    }
}
