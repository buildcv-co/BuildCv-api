using BuildCv.Application.Common;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.FeatureFlags;
using BuildCv.Domain.Payments;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.Invoicing;
using BuildCv.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class BackwardCompatAdaptersTests
{
    [Fact]
    public async Task FeatureFlagInvoiceAdapter_uses_local_provider_when_flag_disabled()
    {
        var flags = new StubFeatureFlag(["factus-enabled"], isEnabled: false);
        var inner = new ThrowingInvoiceAdapter();
        var adapter = new FeatureFlagInvoiceAdapter(flags, inner, NullLogger<FeatureFlagInvoiceAdapter>.Instance);

        var act = async () => await adapter.GetInvoiceAsync("BUILDCV-1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*local*");
    }

    [Fact]
    public async Task FeatureFlagPaymentAdapter_returns_disabled_when_flag_disabled()
    {
        var flags = new StubFeatureFlag(["wompi-enabled"], isEnabled: false);
        var inner = new ThrowingPaymentProvider();
        var adapter = new FeatureFlagPaymentAdapter(flags, inner, NullLogger<FeatureFlagPaymentAdapter>.Instance);

        var act = async () => await adapter.CreateCheckoutAsync(
            "user-1",
            new CreditPackage("starter", 10, 1_500_000, "COP"),
            "idem-1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*disabled*");
    }

    [Fact]
    public void FeatureFlagCreditsAdapter_delegates_to_feature_flag_service()
    {
        var flags = new StubFeatureFlag(["credits-enabled"], isEnabled: true);
        var adapter = new FeatureFlagCreditsAdapter(flags);

        adapter.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagCreditsAdapter_returns_false_when_flag_disabled()
    {
        var flags = new StubFeatureFlag(["credits-enabled"], isEnabled: false);
        var adapter = new FeatureFlagCreditsAdapter(flags);

        adapter.IsEnabled.Should().BeFalse();
    }

    private sealed class StubFeatureFlag : IFeatureFlag
    {
        private readonly HashSet<string> _enabledNames;
        private readonly bool _isEnabled;

        public StubFeatureFlag(string[] enabledNames, bool isEnabled)
        {
            _enabledNames = new HashSet<string>(enabledNames);
            _isEnabled = isEnabled;
        }

        public Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
            => Task.FromResult(_enabledNames.Contains(name) && _isEnabled);

        public Task<FeatureFlag?> GetAsync(string name, CancellationToken ct = default)
            => Task.FromResult<FeatureFlag?>(null);

        public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FeatureFlag>>([]);
    }

    private sealed class ThrowingInvoiceAdapter : IInvoiceProvider
    {
        public Task<BuildCv.Domain.Invoicing.Invoice> CreateInvoiceAsync(
            BuildCv.Domain.Invoicing.Invoice invoice, CancellationToken ct = default)
            => throw new InvalidOperationException("FactusAdapter should not be called when flag is disabled");

        public Task<BuildCv.Domain.Invoicing.Invoice?> GetInvoiceAsync(string number, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<IReadOnlyList<BuildCv.Domain.Invoicing.Invoice>> ListInvoicesAsync(
            int page = 1, int perPage = 20, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task DeleteInvoiceAsync(string referenceCode, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<byte[]> DownloadPdfAsync(string number, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<byte[]> DownloadXmlAsync(string number, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<BuildCv.Domain.Invoicing.Invoice> CreateCreditNoteAsync(
            BuildCv.Domain.Invoicing.Invoice invoice, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<BuildCv.Domain.Invoicing.Invoice> CreateSupportDocumentAsync(
            BuildCv.Domain.Invoicing.Invoice invoice, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<IReadOnlyList<BuildCv.Domain.Invoicing.NumberingRange>> GetNumberingRangesAsync(
            CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<BuildCv.Domain.Invoicing.NumberingRange> CreateNumberingRangeAsync(
            BuildCv.Domain.Invoicing.NumberingRange range, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<BuildCv.Domain.Invoicing.CompanyInfo> GetCompanyAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");

        public Task<BuildCv.Domain.Invoicing.CompanyInfo> UpdateCompanyAsync(
            BuildCv.Domain.Invoicing.CompanyInfo company, CancellationToken ct = default)
            => throw new InvalidOperationException("local fallback should have been used");
    }

    private sealed class ThrowingPaymentProvider : IPaymentProvider
    {
        public Task<CheckoutSession> CreateCheckoutAsync(
            string userId, CreditPackage package, string idempotencyKey, CancellationToken ct = default)
            => throw new InvalidOperationException("WompiAdapter should not be called when flag is disabled");

        public Task<TransactionStatus?> GetTransactionStatusAsync(
            string wompiTransactionId, CancellationToken ct = default)
            => throw new InvalidOperationException("WompiAdapter should not be called when flag is disabled");

        public bool VerifyWebhookSignature(string payload, string signatureHeader) => false;
    }
}
