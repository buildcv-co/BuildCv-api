# Contracts: 002-score-engine

## HTTP Contracts

### POST /api/v1/score (sincrónico)

```http
POST /api/v1/score HTTP/1.1
Host: api.buildcv.app
Content-Type: application/json
```

**Request Body**:
```json
{
  "cvText": "string (max 20000)",
  "jobText": "string (max 20000)"
}
```

**Response 200 OK**:
```json
{
  "score": 78,
  "band": "Strong",
  "components": [
    {
      "code": "skill_match",
      "weight": 0.6,
      "value": 0.85,
      "rationale": "8/10 skills matched (C1: 5, C2: 2, C3: 1)"
    },
    {
      "code": "format_legibility",
      "weight": 0.2,
      "value": 0.7,
      "rationale": "CV has 4 sections, 1.2k chars per section avg"
    },
    {
      "code": "experience_relevance",
      "weight": 0.2,
      "value": 0.6,
      "rationale": "2 years relevant experience, but job asks 5+"
    }
  ],
  "present": ["C#", ".NET", "SQL", "Git"],
  "missing": ["AWS", "Docker", "Kubernetes"],
  "engineVersion": "1.0.0"
}
```

**Response 400 Bad Request** (validation):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "CvText": ["The field CvText must be a string with a maximum length of 20000."],
    "JobText": ["The field JobText must be a string with a maximum length of 20000."]
  }
}
```

**Response 429 Too Many Requests** (rate-limit "score" 60/min):
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Has alcanzado el tope de análisis (60/minuto).",
  "instance": "/api/v1/score",
  "retryAfter": "2026-06-08T16:30:00Z"
}
```

## Domain Contracts (C# interfaces)

### IScoringEngine

```csharp
namespace BuildCv.Domain.Scoring;

public interface IScoringEngine
{
    ScoreResult Score(JobRequirements job, CvProfile cv);
}
```

### ISkillMatcher

```csharp
namespace BuildCv.Domain.Scoring;

public interface ISkillMatcher
{
    MatchResult Match(IReadOnlyList<string> jobSkills, IReadOnlyList<string> cvSkills);
}
```

## Configuration Contract

```json
{
  "RateLimit": {
    "Score": {
      "PermitLimit": 60,
      "Window": "00:01:00"
    }
  }
}
```

## Logging Contract (Serilog structured)

```csharp
// ✓ Allowed
Log.Information("Score completed (cvLength={CvLen}, jobLength={JobLen}, score={Score}, engineVersion={EngineVersion}, traceId={TraceId})",
    cv.Length, job.Length, result.Score, result.EngineVersion, traceId);

// ✗ Prohibited (Constitution Art. III NFR-002)
Log.Information("CV: {Cv}", cv);     // NUNCA contenido
Log.Information("Job: {Job}", job); // NUNCA contenido
```
