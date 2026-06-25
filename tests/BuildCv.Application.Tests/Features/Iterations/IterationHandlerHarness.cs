using BuildCv.Application.Features.Adapt;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Iterations;
using BuildCv.Application.Features.Scoring;
using BuildCv.Application.Tests.Adapt;
using BuildCv.Application.Tests.Credits;
using BuildCv.Domain.Adapt;
using BuildCv.Domain.Jobs;
using BuildCv.Domain.Lexicon;
using BuildCv.Domain.Resumes;
using BuildCv.Domain.Scoring;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Application.Tests.Features.Iterations;

internal sealed class IterationHandlerHarness
{
    public TestIterationStore Store { get; } = new();
    public TestCreditLedger Ledger { get; } = new();
    public ScriptedAiClient Ai { get; } = new();
    public ScriptedScoringEngine Scoring { get; } = new();
    public TimeSpan? PerIterationTimeoutOverride { get; set; }
    public TimeSpan? TotalTimeoutOverride { get; set; }

    public IterateAdaptationHandler BuildHandler()
    {
        var extractor = new EntityExtractor(new IterationTestGazetteer());
        var crossValidator = new CrossEntityValidator();
        var severityPolicy = new SeverityPolicy();
        var promptBuilder = new PromptBuilder();
        var adaptHandler = new AdaptCvHandler(Ai, extractor, crossValidator, severityPolicy, promptBuilder, NullLogger<AdaptCvHandler>.Instance);
        var scoreHandler = new ScoreCvHandler(new NoopJobAnalyzer(), new NoopCvAnalyzer(), Scoring);

        return new IterateAdaptationHandler(
            adaptHandler,
            scoreHandler,
            crossValidator,
            extractor,
            Store,
            Ledger,
            NullLogger<IterateAdaptationHandler>.Instance,
            perIterationTimeout: PerIterationTimeoutOverride,
            totalTimeout: TotalTimeoutOverride);
    }
}

internal sealed class ScriptedAiClient : IAiClient
{
    private readonly Queue<string> _responses = new();
    private readonly HashSet<int> _throwOnCallIndices = new();

    public IReadOnlyList<string> Calls => _calls;
    private readonly List<string> _calls = new();

    public TimeSpan PerCallDelay { get; set; } = TimeSpan.Zero;

    public void Enqueue(string adaptedCv) => _responses.Enqueue(adaptedCv);
    public void EnqueueMany(params string[] adaptedCvs)
    {
        foreach (var c in adaptedCvs)
        {
            _responses.Enqueue(c);
        }
    }

    public void ThrowCancellationOnCall(int zeroBasedIndex)
    {
        _throwOnCallIndices.Add(zeroBasedIndex);
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var callIndex = _calls.Count;
        _calls.Add(prompt);

        if (PerCallDelay > TimeSpan.Zero)
        {
            await Task.Delay(PerCallDelay, ct);
        }

        if (_throwOnCallIndices.Contains(callIndex))
        {
            throw new OperationCanceledException("simulated per-iteration timeout");
        }

        if (_responses.Count == 0)
        {
            return "Adapted CV (default)";
        }
        return _responses.Dequeue();
    }

    public Task<T> CompleteStructuredAsync<T>(string prompt, CancellationToken ct) where T : class
    {
        var adapted = CompleteAsync(prompt, ct).GetAwaiter().GetResult();
        if (typeof(T) == typeof(BuildCv.Application.Features.Adapt.AdaptationResponse))
        {
            var typed = (T)(object)new BuildCv.Application.Features.Adapt.AdaptationResponse
            {
                AdaptedText = adapted,
                Reasoning = "scripted",
                AddedEntities = Array.Empty<string>(),
                RemovedEntities = Array.Empty<string>()
            };
            return Task.FromResult(typed);
        }
        throw new NotSupportedException($"ScriptedAiClient no implementa {typeof(T).Name}");
    }
}

internal sealed class ScriptedScoringEngine : IScoringEngine
{
    public Func<int, int>? ScoreSelector { get; set; }

    public ScoreResult Score(JobRequirementSet job, CvAnalysis cv)
    {
        var score = ScoreSelector?.Invoke(_call++) ?? 50;
        return BuildResult(score);
    }

    private int _call;

    private static ScoreResult BuildResult(int overall) => new(
        Overall: overall,
        Band: ScoreBand.Medio,
        Disclaimer: "test disclaimer",
        Components: Array.Empty<ComponentScore>(),
        Keywords: new KeywordAnalysis(Array.Empty<KeywordView>(), Array.Empty<KeywordView>(), Array.Empty<KeywordView>()),
        Recommendations: Array.Empty<Recommendation>(),
        FormatIssues: Array.Empty<FormatIssue>(),
        GatesApplied: Array.Empty<GateApplied>(),
        EngineVersion: "test-1.0.0",
        LexiconVersion: "test",
        ContextHash: "test-hash");
}

internal sealed class NoopJobAnalyzer : IJobAnalyzer
{
    public JobRequirementSet Analyze(string jobText) =>
        new(Array.Empty<BuildCv.Domain.Jobs.Requirement>(), "noop-job-hash");
}

internal sealed class NoopCvAnalyzer : ICvAnalyzer
{
    public CvAnalysis Analyze(string cvText) => new(
        Profile: new CvProfile(
            SkillPlacements: new Dictionary<string, Placement>(),
            Tokens: new HashSet<string>(),
            Stems: new HashSet<string>()),
        SectionsPresent: new HashSet<string>(),
        HasContact: false,
        HasExperience: false,
        ActionVerbCount: 0,
        QuantifiedAchievementCount: 0,
        WordCount: 0,
        MaxSkillRepetition: 0);
}

internal sealed class IterationTestGazetteer : ISkillGazetteer
{
    public string Version => "test";

    public bool TryResolve(string normalizedToken, out SkillEntry entry)
    {
        entry = null!;
        return false;
    }

    public bool TryGetById(string canonicalId, out SkillEntry entry)
    {
        entry = null!;
        return false;
    }

    public IReadOnlyList<string> Related(string canonicalId) => Array.Empty<string>();

    public IReadOnlyList<string> Implies(string canonicalId) => Array.Empty<string>();

    public bool AreConfusable(string a, string b) => false;
}
