using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BuildCv.Application.Features.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Payments;

public sealed class WompiRecurringAdapter : ISubscriptionProvider
{
    private readonly HttpClient _httpClient;
    private readonly WompiSettings _settings;
    private readonly ILogger<WompiRecurringAdapter> _logger;

    public WompiRecurringAdapter(
        HttpClient httpClient,
        IOptions<WompiSettings> settings,
        ILogger<WompiRecurringAdapter> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> CreateScheduledChargeAsync(
        string paymentSourceId,
        decimal amountCop,
        string currency,
        DateTime chargeDate,
        CancellationToken ct = default)
    {
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.PrivateKey);

        var body = new
        {
            payment_source_id = paymentSourceId,
            amount_in_cents = (long)(amountCop * 100),
            currency,
            charge_date = chargeDate.ToString("yyyy-MM-dd"),
        };
        request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WompiSubscriptionResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Wompi returned an empty response");

        _logger.LogInformation(
            "Wompi subscription scheduled for payment source {PaymentSourceId} amount {Amount} {Currency} charge {ChargeDate}: {SubscriptionId}",
            paymentSourceId, amountCop, currency, chargeDate, payload.Data.Id);

        return payload.Data.Id;
    }

    public async Task<bool> CancelScheduledChargeAsync(string chargeId, CancellationToken ct = default)
    {
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        }

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/subscriptions/{chargeId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.PrivateKey);

        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public bool VerifyWebhookSignature(string payload, string signature)
    {
        return WompiHmac.Verify(_settings.WebhookSecret, payload, signature);
    }
}

internal sealed record WompiSubscriptionResponse(
    [property: JsonPropertyName("data")] WompiSubscriptionData Data);

internal sealed record WompiSubscriptionData(
    [property: JsonPropertyName("id")] string Id);
