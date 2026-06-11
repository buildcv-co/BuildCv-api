using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildCv.Application.Features.Payments;
using BuildCv.Domain.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildCv.Infrastructure.Payments;

public sealed class WompiAdapter : IPaymentProvider
{
    private readonly HttpClient _http;
    private readonly WompiSettings _settings;
    private readonly ILogger<WompiAdapter> _logger;

    public WompiAdapter(HttpClient http, IOptions<WompiSettings> settings, ILogger<WompiAdapter> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(_settings.BaseUrl);
        }
    }

    public async Task<CheckoutSession> CreateCheckoutAsync(
        string userId,
        CreditPackage package,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/merchants/{_settings.PublicKey}/payment_links");

        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        var body = new
        {
            name = $"BuildCV Credits — {package.Id}",
            description = $"{package.Credits} BuildCV credits",
            single_use = true,
            collect_shipping = false,
            currency = package.Currency,
            amount_in_cents = package.PriceInCents,
            reference = idempotencyKey,
            redirect_url = $"https://buildcv.com/payments/return?ref={idempotencyKey}",
        };

        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WompiPaymentLinkResponse>(ct)
            ?? throw new InvalidOperationException("Wompi returned an empty response");

        _logger.LogInformation(
            "Wompi checkout created for user {UserId} package {PackageId} session {SessionId}",
            userId,
            package.Id,
            payload.Data.Id);

        return new CheckoutSession
        {
            SessionId = payload.Data.Id,
            PublicKey = _settings.PublicKey,
            AmountInCents = payload.Data.AmountInCents,
            Currency = payload.Data.Currency,
            Reference = payload.Data.Reference,
        };
    }

    public async Task<TransactionStatus?> GetTransactionStatusAsync(
        string wompiTransactionId,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/transactions/{wompiTransactionId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.PrivateKey);

        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WompiTransactionResponse>(ct)
            ?? throw new InvalidOperationException("Wompi returned an empty response");

        return new TransactionStatus
        {
            WompiTransactionId = payload.Data.Id,
            Status = payload.Data.Status,
            AmountInCents = payload.Data.AmountInCents,
        };
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(_settings.WebhookSecret))
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(_settings.WebhookSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var computed = hmac.ComputeHash(payloadBytes);
        var expected = HexToBytes(signatureHeader);

        if (expected.Length != computed.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    private static byte[] HexToBytes(string hex)
    {
        var length = hex.Length;
        if ((length & 1) != 0)
        {
            return [];
        }

        var bytes = new byte[length / 2];
        for (var i = 0; i < length; i += 2)
        {
            if (!byte.TryParse(hex.AsSpan(i, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i / 2]))
            {
                return [];
            }
        }

        return bytes;
    }
}

internal sealed record WompiPaymentLinkResponse
{
    [JsonPropertyName("data")]
    public WompiPaymentLinkData Data { get; init; } = new();
}

internal sealed record WompiPaymentLinkData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("amount_in_cents")]
    public long AmountInCents { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "COP";

    [JsonPropertyName("reference")]
    public string Reference { get; init; } = "";
}

internal sealed record WompiTransactionResponse
{
    [JsonPropertyName("data")]
    public WompiTransactionData Data { get; init; } = new();
}

internal sealed record WompiTransactionData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("amount_in_cents")]
    public long AmountInCents { get; init; }
}
