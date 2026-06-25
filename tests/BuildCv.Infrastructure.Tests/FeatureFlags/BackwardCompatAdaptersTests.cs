using BuildCv.Application.Common;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.FeatureFlags;
using BuildCv.Domain.Invoicing;
using BuildCv.Domain.Payments;
using BuildCv.Infrastructure.Credits;
using BuildCv.Infrastructure.Invoicing;
using BuildCv.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Tests.FeatureFlags;

public sealed class BackwardCompatAdaptersTests
{
    [Fact]
    public async Task FeatureFlagInvoiceAdapter_delegates_to_LocalInvoiceProvider_when_flag_disabled()
    {
        var flags = new StubFeatureFlag(["factus-enabled"], isEnabled: false);
        var invoiceStore = new InMemoryInvoiceStore();
        var numberingStore = new InMemoryNumberingRangeStore();
        var localProvider = new LocalInvoiceProvider(invoiceStore, numberingStore, NullLogger<LocalInvoiceProvider>.Instance);
        var factusAdapter = CreateFactusAdapter();
        var adapter = new FeatureFlagInvoiceAdapter(flags, factusAdapter, localProvider);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferenceCode = "REF-DISABLED",
            AmountInCents = 150000,
            Currency = "COP",
            Status = InvoiceStatus.Draft,
            CustomerName = "Test User",
            CustomerIdentification = "1234567890",
            CustomerEmail = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await adapter.CreateInvoiceAsync(invoice);

        result.Number.Should().StartWith("BUILDCV-");
        result.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task FeatureFlagPaymentAdapter_delegates_to_DisabledPaymentProvider_when_flag_disabled()
    {
        var flags = new StubFeatureFlag(["wompi-enabled"], isEnabled: false);
        var disabledProvider = new DisabledPaymentProvider(NullLogger<DisabledPaymentProvider>.Instance);
        var wompiAdapter = CreateWompiAdapter();
        var adapter = new FeatureFlagPaymentAdapter(flags, wompiAdapter, disabledProvider);

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

    private static FactusAdapter CreateFactusAdapter()
    {
        var settings = new FactusSettings
        {
            Enabled = true,
            BaseUrl = "https://factus.local",
            ClientId = "test",
            ClientSecret = "test",
            Email = "test@example.com",
            Password = "test"
        };
        return new FactusAdapter(new HttpClient(), Options.Create(settings), NullLogger<FactusAdapter>.Instance);
    }

    private static WompiAdapter CreateWompiAdapter()
    {
        var settings = new WompiSettings
        {
            Enabled = true,
            Environment = "sandbox",
            PublicKey = "pub_test",
            PrivateKey = "prv_test",
            WebhookSecret = "secret"
        };
        return new WompiAdapter(new HttpClient(), Options.Create(settings), NullLogger<WompiAdapter>.Instance);
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
}
