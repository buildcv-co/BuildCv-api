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
            Purposes: ["CV scoring", "Readability analysis"]),
        new(
            Version: 2,
            Content: """
                BuildCV Privacy Policy v2 — Effective 2026-06-25

                Section 1: Data We Store (v1)
                We do not store your CV or job description content. Scoring and adaptation operate on data that lives only in your browser session.

                Section 2: Account Data (NEW v2)
                When you sign in via Google or LinkedIn, we store your email, display name, and OAuth provider ID. You can request deletion at any time (Habeas Data / ARCO right).

                Section 3: Credit Balance (NEW v2)
                We store your current credit balance in our database. Each credit represents one CV adaptation. Credit grants and consumption are recorded in an append-only audit ledger so we can answer "why did my balance change?" with certainty. Your credit balance is never shared with third parties.

                Section 4: Payments and Invoices (NEW v2)
                When you purchase credits, the payment is processed by Wompi (PCI-DSS compliant, server-to-server webhook). For Colombian tax compliance (DIAN), electronic invoices are issued and retained for the legally required period, even if you later exercise your ARCO right to be forgotten. In that case, your user record is anonymized (email and name replaced with "[deleted]@anonymized" / "[Deleted User]"), but the invoice and its associated payment remain in our records as required by Colombian tax law.

                Section 5: ARCO Rights (NEW v2)
                You have the right to Access, Rectify, Cancel, and Oppose (ARCO) your personal data. Submit a request via the dashboard. Anonymization or deletion is processed within 15 business days as required by Colombian law (Ley 1581 de 2012).

                Section 6: No Tracking (unchanged from v1)
                We do not use cookies, third-party analytics, fingerprinting, or behavioral tracking. Your usage data stays on your device and our servers.
                """,
            EffectiveDate: new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc),
            DataCategories:
            [
                "Profile (name, email, OAuth provider ID)",
                "Credit balance (integer)",
                "Credit ledger entries (append-only audit log)",
                "Payment records (Wompi webhook metadata)",
                "Electronic invoices (DIAN legal hold)"
            ],
            Purposes:
            [
                "Account authentication and identification",
                "Credit balance tracking and audit",
                "Payment processing and Colombian tax compliance (DIAN)",
                "CV scoring and readability analysis"
            ])
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
