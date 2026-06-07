using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Text;

namespace BuildCv.Domain.Scoring;

/// <summary>
/// Cascada de coincidencia determinista (D01/D02): exacto → implicación → relación →
/// lema/stem → fuzzy blindado. El crédito final = crédito de nivel × factor de ubicación.
/// </summary>
public sealed class SkillMatcher : ISkillMatcher
{
    private const double FuzzyThreshold = 0.92;

    private readonly ISkillGazetteer _gazetteer;
    private readonly ISpanishStemmer _stemmer;
    private readonly ITextNormalizer _normalizer;
    private readonly ConfusableBlocklist _confusables;

    public SkillMatcher(
        ISkillGazetteer gazetteer,
        ISpanishStemmer stemmer,
        ITextNormalizer normalizer,
        ConfusableBlocklist confusables)
    {
        _gazetteer = gazetteer;
        _stemmer = stemmer;
        _normalizer = normalizer;
        _confusables = confusables;
    }

    public MatchResult Match(Requirement requirement, CvProfile cv)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(cv);

        // T0 — Exacto: el requisito está presente como skill del CV.
        if (cv.SkillPlacements.TryGetValue(requirement.CanonicalId, out var placement))
        {
            return Make(requirement, MatchTier.Exact, placement, tierCredit: 1.0);
        }

        // T1 — Implicación ascendente: un skill del CV implica el requisito
        // (p. ej. ASP.NET Core ⇒ .NET). El requisito queda satisfecho de pleno.
        foreach (var (cvId, place) in cv.SkillPlacements)
        {
            if (_gazetteer.Implies(cvId).Contains(requirement.CanonicalId))
            {
                return Make(requirement, MatchTier.Alias, place, tierCredit: 1.0);
            }
        }

        // T3 — Relación o implicación descendente: skill adyacente o más general
        // (tener .NET no garantiza ASP.NET Core) → crédito parcial.
        foreach (var (cvId, place) in cv.SkillPlacements)
        {
            if (_gazetteer.Related(requirement.CanonicalId).Contains(cvId)
                || _gazetteer.Related(cvId).Contains(requirement.CanonicalId)
                || _gazetteer.Implies(requirement.CanonicalId).Contains(cvId))
            {
                return Make(requirement, MatchTier.Related, place, tierCredit: 0.5);
            }
        }

        // T2 — Lema/stem: para keywords genéricas no resueltas por el gazetteer.
        var term = _normalizer.Normalize(requirement.Display);
        var termStem = _stemmer.Stem(term);
        if (termStem.Length > 0 && cv.Stems.Contains(termStem))
        {
            return Make(requirement, MatchTier.Lemma, Placement.Buried, tierCredit: 0.85);
        }

        // T4 — Fuzzy: mejor Jaro-Winkler contra los tokens, con blindaje de confundibles.
        var best = 0.0;
        foreach (var token in cv.Tokens)
        {
            if (_confusables.AreConfusable(term, token)
                || _gazetteer.AreConfusable(requirement.CanonicalId, token))
            {
                continue;
            }

            var similarity = StringSimilarity.JaroWinkler(term, token);
            if (similarity > best)
            {
                best = similarity;
            }
        }

        return best >= FuzzyThreshold
            ? Make(requirement, MatchTier.Fuzzy, Placement.Buried, tierCredit: 0.85 * best)
            : Make(requirement, MatchTier.None, Placement.NotFound, tierCredit: 0.0);
    }

    private static MatchResult Make(Requirement requirement, MatchTier tier, Placement placement, double tierCredit)
    {
        var locationFactor = placement switch
        {
            Placement.Prominent => 1.0,
            Placement.Buried => 0.6,
            _ => 0.0,
        };

        var credit = Math.Clamp(tierCredit * locationFactor, 0.0, 1.0);
        return new MatchResult(requirement, tier, placement, credit, EvidenceSnippet: null);
    }
}
