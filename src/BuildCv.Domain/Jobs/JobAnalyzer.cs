using System.Security.Cryptography;
using System.Text;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;

namespace BuildCv.Domain.Jobs;

/// <summary>Extrae el conjunto de requisitos de una vacante (determinista, sin LLM).</summary>
public interface IJobAnalyzer
{
    JobRequirementSet Analyze(string jobText);
}

/// <summary>
/// Detecta secciones de la vacante, resuelve skills por sección y construye requisitos
/// ponderados por <c>categoría × sección × frecuencia</c> (FR-014). Sella un
/// <see cref="JobRequirementSet.ContextHash"/> reproducible (FR-031).
/// </summary>
public sealed class JobAnalyzer(
    SectionSplitter splitter,
    SkillScanner scanner,
    ISkillGazetteer gazetteer) : IJobAnalyzer
{
    private static readonly Dictionary<string, string> Headers = new()
    {
        ["requisitos"] = "must",
        ["requerimientos"] = "must",
        ["requisito"] = "must",
        ["perfil"] = "must",
        ["que buscamos"] = "must",
        ["lo que buscamos"] = "must",
        ["deseable"] = "nice",
        ["deseables"] = "nice",
        ["valoramos"] = "nice",
        ["nice to have"] = "nice",
        ["responsabilidades"] = "resp",
        ["funciones"] = "resp",
        ["actividades"] = "resp",
    };

    public JobRequirementSet Analyze(string jobText)
    {
        ArgumentNullException.ThrowIfNull(jobText);

        var sections = splitter.Split(jobText, Headers, preambleLabel: "title");
        var bySkill = new Dictionary<string, SkillOccurrence>(StringComparer.Ordinal);

        foreach (var section in sections)
        {
            var requirementSection = MapSection(section.Label);
            foreach (var (id, count) in scanner.Scan(section.Body))
            {
                if (bySkill.TryGetValue(id, out var existing))
                {
                    var best = Multiplier(requirementSection) > Multiplier(existing.Section)
                        ? requirementSection
                        : existing.Section;
                    bySkill[id] = new SkillOccurrence(best, existing.Count + count);
                }
                else
                {
                    bySkill[id] = new SkillOccurrence(requirementSection, count);
                }
            }
        }

        var requirements = bySkill
            .Select(pair => BuildRequirement(pair.Key, pair.Value))
            .Where(requirement => requirement is not null)
            .Select(requirement => requirement!)
            .OrderByDescending(requirement => requirement.Weight)
            .ThenBy(requirement => requirement.CanonicalId, StringComparer.Ordinal)
            .ToList();

        return new JobRequirementSet(requirements, ComputeContextHash(requirements));
    }

    private Requirement? BuildRequirement(string id, SkillOccurrence occurrence)
    {
        if (!gazetteer.TryGetById(id, out var entry))
        {
            return null;
        }

        var weight = Math.Clamp(
            BaseWeight(entry.Category) * Multiplier(occurrence.Section) * FrequencyFactor(occurrence.Count),
            0.2,
            2.0);

        return new Requirement(id, entry.Canonical, entry.Category, occurrence.Section, weight);
    }

    private static RequirementSection MapSection(string label) => label switch
    {
        "must" => RequirementSection.MustHave,
        "nice" => RequirementSection.NiceToHave,
        "resp" => RequirementSection.Responsibility,
        _ => RequirementSection.Title,
    };

    private static double Multiplier(RequirementSection section) => section switch
    {
        RequirementSection.MustHave => 1.2,
        RequirementSection.Title => 1.1,
        RequirementSection.Responsibility => 1.0,
        _ => 0.6,
    };

    private static double BaseWeight(SkillCategory category) => category switch
    {
        SkillCategory.HardSkill => 1.0,
        SkillCategory.Tool => 0.9,
        SkillCategory.SoftSkill => 0.6,
        _ => 0.5,
    };

    private static double FrequencyFactor(int count) => Math.Min(1.3, 1.0 + (0.1 * (count - 1)));

    private static string ComputeContextHash(IReadOnlyList<Requirement> requirements)
    {
        var canonical = string.Join(
            "|",
            requirements
                .OrderBy(requirement => requirement.CanonicalId, StringComparer.Ordinal)
                .Select(requirement => $"{requirement.CanonicalId}:{requirement.Weight:F2}:{requirement.Section}"));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }

    private readonly record struct SkillOccurrence(RequirementSection Section, int Count);
}
