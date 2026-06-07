using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BuildCv.Infrastructure.Lexicon;

/// <summary>
/// Carga el diccionario de habilidades desde el YAML embebido y construye el
/// <see cref="SkillGazetteer"/> de dominio. Mantiene el dominio puro: el IO de
/// recursos y la dependencia de YamlDotNet viven aquí, en Infrastructure.
/// </summary>
public static class GazetteerLoader
{
    private const string ResourceSuffix = "skills.gazetteer.v1.yaml";

    public static SkillGazetteer LoadEmbedded()
    {
        var assembly = typeof(GazetteerLoader).Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Recurso embebido no encontrado: {ResourceSuffix}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se pudo abrir el recurso: {resourceName}");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    public static SkillGazetteer Parse(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var file = deserializer.Deserialize<GazetteerFile>(yaml)
            ?? throw new InvalidOperationException("El YAML del gazetteer está vacío o es inválido.");

        var entries = file.Skills.ConvertAll(ToEntry);
        return new SkillGazetteer(file.Version, entries, new SpanishTextNormalizer());
    }

    private static SkillEntry ToEntry(SkillEntryDto dto) => new(
        dto.Id,
        dto.Canonical,
        MapCategory(dto.Category),
        dto.Aliases ?? [],
        dto.Implies ?? [],
        dto.Related ?? [],
        dto.Broader ?? [],
        dto.ConfusableWith ?? []);

    private static SkillCategory MapCategory(string? category) => category switch
    {
        "hardSkill" => SkillCategory.HardSkill,
        "tool" => SkillCategory.Tool,
        "softSkill" => SkillCategory.SoftSkill,
        _ => SkillCategory.GenericKeyword,
    };

    private sealed class GazetteerFile
    {
        public string Version { get; set; } = string.Empty;
        public List<SkillEntryDto> Skills { get; set; } = [];
    }

    private sealed class SkillEntryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Canonical { get; set; } = string.Empty;
        public string? Category { get; set; }
        public List<string>? Aliases { get; set; }
        public List<string>? Implies { get; set; }
        public List<string>? Related { get; set; }
        public List<string>? Broader { get; set; }
        public List<string>? ConfusableWith { get; set; }
    }
}
