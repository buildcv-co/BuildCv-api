namespace BuildCv.Application.Features.Consent;

public sealed record PrivacyPolicyQuery(int? Version = null);

public sealed record PrivacyPolicyResponse(int Version, string Content, DateTime EffectiveDate, IReadOnlyList<string> DataCategories, IReadOnlyList<string> Purposes);
