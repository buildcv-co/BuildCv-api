using System.Text.RegularExpressions;
using BuildCv.Domain.Scoring;
using BuildCv.Domain.Text;

namespace BuildCv.Domain.Resumes;

/// <summary>Analiza el CV de forma determinista (secciones, skills, logros, contacto).</summary>
public interface ICvAnalyzer
{
    CvAnalysis Analyze(string cvText);
}

/// <summary>
/// Detecta secciones, ubica los skills (prominentes vs enterrados), y extrae señales de
/// logros (verbos de acción + métricas cuantificadas) y contacto. Todo sin IO ni LLM.
/// </summary>
public sealed class CvAnalyzer : ICvAnalyzer
{
    private static readonly Regex EmailRegex = new(@"[\w.\-+]+@[\w\-]+\.[\w.\-]+", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"\+?\d[\d \-().]{6,}\d", RegexOptions.Compiled);
    private static readonly Regex YearRegex = new(@"\b(?:19|20)\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex DigitRegex = new(@"\d", RegexOptions.Compiled);

    private static readonly HashSet<string> ProminentSections =
        new(["skills", "summary", "certifications", "header"], StringComparer.Ordinal);

    private static readonly string[] VerbSeeds =
    [
        "lidere", "liderar", "desarrolle", "desarrollar", "implemente", "implementar",
        "disene", "disenar", "optimice", "optimizar", "reduje", "reducir",
        "aumente", "aumentar", "construi", "construir", "gestione", "gestionar",
        "automatice", "automatizar", "migre", "migrar", "mejore", "mejorar",
        "coordine", "coordinar", "dirigi", "dirigir", "cree", "crear",
        "lance", "lanzar", "entregue", "entregar", "administre", "administrar",
        "integre", "integrar", "configure", "configurar", "analice", "analizar",
        "ejecute", "ejecutar", "lidero", "logre", "lograr",
    ];

    private static readonly Dictionary<string, string> Headers = new()
    {
        ["experiencia"] = "experience",
        ["experiencia laboral"] = "experience",
        ["experiencia profesional"] = "experience",
        ["trayectoria"] = "experience",
        ["educacion"] = "education",
        ["formacion"] = "education",
        ["formacion academica"] = "education",
        ["estudios"] = "education",
        ["habilidades"] = "skills",
        ["competencias"] = "skills",
        ["conocimientos"] = "skills",
        ["aptitudes"] = "skills",
        ["skills"] = "skills",
        ["tecnologias"] = "skills",
        ["stack tecnologico"] = "skills",
        ["perfil"] = "summary",
        ["perfil profesional"] = "summary",
        ["resumen"] = "summary",
        ["sobre mi"] = "summary",
        ["acerca de mi"] = "summary",
        ["objetivo"] = "summary",
        ["contacto"] = "contact",
        ["datos de contacto"] = "contact",
        ["idiomas"] = "languages",
        ["proyectos"] = "projects",
        ["certificaciones"] = "certifications",
        ["certificados"] = "certifications",
        ["cursos"] = "certifications",
    };

    private readonly SectionSplitter _splitter;
    private readonly SkillScanner _scanner;
    private readonly ITextNormalizer _normalizer;
    private readonly ISpanishStemmer _stemmer;
    private readonly HashSet<string> _actionVerbStems;

    public CvAnalyzer(
        SectionSplitter splitter,
        SkillScanner scanner,
        ITextNormalizer normalizer,
        ISpanishStemmer stemmer)
    {
        _splitter = splitter;
        _scanner = scanner;
        _normalizer = normalizer;
        _stemmer = stemmer;
        _actionVerbStems = VerbSeeds.Select(stemmer.Stem).ToHashSet(StringComparer.Ordinal);
    }

    public CvAnalysis Analyze(string cvText)
    {
        ArgumentNullException.ThrowIfNull(cvText);

        var sections = _splitter.Split(cvText, Headers, preambleLabel: "header");
        var placements = new Dictionary<string, Placement>(StringComparer.Ordinal);
        var sectionsPresent = new HashSet<string>(StringComparer.Ordinal);
        var totalCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var section in sections)
        {
            if (section.Label is not "header" and not "other")
            {
                sectionsPresent.Add(section.Label);
            }

            var prominent = ProminentSections.Contains(section.Label);
            foreach (var (id, count) in _scanner.Scan(section.Body))
            {
                if (prominent)
                {
                    placements[id] = Placement.Prominent;
                }
                else if (!placements.ContainsKey(id))
                {
                    placements[id] = Placement.Buried;
                }

                totalCounts[id] = totalCounts.GetValueOrDefault(id) + count;
            }
        }

        var normalized = _normalizer.Normalize(cvText);
        string[] tokens = normalized.Length == 0 ? [] : normalized.Split(' ');
        var tokenSet = tokens.ToHashSet(StringComparer.Ordinal);
        var stemSet = tokens.Select(_stemmer.Stem).ToHashSet(StringComparer.Ordinal);

        var profile = new CvProfile(placements, tokenSet, stemSet);

        return new CvAnalysis(
            profile,
            sectionsPresent,
            HasContact: EmailRegex.IsMatch(cvText) || PhoneRegex.IsMatch(cvText) || sectionsPresent.Contains("contact"),
            HasExperience: sectionsPresent.Contains("experience") || YearRegex.IsMatch(cvText),
            ActionVerbCount: CountActionVerbs(tokens),
            QuantifiedAchievementCount: CountQuantifiedAchievements(cvText),
            WordCount: tokens.Length,
            MaxSkillRepetition: totalCounts.Count == 0 ? 0 : totalCounts.Values.Max());
    }

    private int CountActionVerbs(IReadOnlyList<string> tokens)
    {
        var count = 0;
        foreach (var token in tokens)
        {
            if (_actionVerbStems.Contains(_stemmer.Stem(token)))
            {
                count++;
            }
        }

        return count;
    }

    private int CountQuantifiedAchievements(string cvText)
    {
        var count = 0;
        foreach (var line in cvText.Split('\n'))
        {
            if (DigitRegex.IsMatch(line) && LineHasActionVerb(line))
            {
                count++;
            }
        }

        return count;
    }

    private bool LineHasActionVerb(string line)
    {
        foreach (var token in _normalizer.Tokenize(line))
        {
            if (_actionVerbStems.Contains(_stemmer.Stem(token)))
            {
                return true;
            }
        }

        return false;
    }
}
