using BuildCv.Domain.Scoring;

namespace BuildCv.Application.Features.Scoring;

/// <summary>
/// Discriminador del resultado del <see cref="ScoreCvHandler"/>. La rama v1
/// preserva el <see cref="ScoreResult"/> legacy; la rama v2 lleva el
/// <see cref="ScoreResultV2"/> con <c>PerSection</c> + <c>RedFlags</c>.
/// El consumidor (endpoint + mapper) hace pattern-match sobre el tipo
/// concreto para emitir el contrato correcto (Constitution Art. VI:
/// Domain PURO + Art. II: motor determinista sellado por versión).
/// </summary>
public abstract record ScoreOutcome;

public sealed record V1ScoreOutcome(ScoreResult Result) : ScoreOutcome;

public sealed record V2ScoreOutcome(ScoreResultV2 Result) : ScoreOutcome;
