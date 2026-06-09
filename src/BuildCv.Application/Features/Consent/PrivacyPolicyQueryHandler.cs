namespace BuildCv.Application.Features.Consent;

public sealed class PrivacyPolicyQueryHandler
{
    private static readonly PrivacyPolicyResponse[] Policies =
    [
        new(
            Version: 1,
            Content: """
                BuildCv Privacy Policy (v1)

                1. Data We Collect: Profile information (name, email) provided via OAuth authentication.
                2. Purpose: CV scoring and readability analysis against job descriptions.
                3. Data Retention: No CV or job data is stored in v0. Profile data persists only in memory.
                4. Your Rights (ARCO): Access, rectify, or cancel your data at any time.
                5. Consent: You may grant or revoke consent for data processing at any time.
                6. No Sale of Data: We never sell or share personal data with third parties.
                """,
            EffectiveDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DataCategories: ["Profile (name, email)", "OAuth provider identity"],
            Purposes: ["CV scoring", "Readability analysis"])
    ];

    public Task<PrivacyPolicyResponse> HandleAsync(PrivacyPolicyQuery query, CancellationToken ct)
    {
        var version = query.Version ?? Policies.MaxBy(p => p.Version)!.Version;
        var policy = Policies.FirstOrDefault(p => p.Version == version);

        if (policy is null)
        {
            throw new KeyNotFoundException($"Privacy policy version {version} not found.");
        }

        return Task.FromResult(policy);
    }
}
