using BuildCv.Domain.Common;
using BuildCv.Domain.Payments;

namespace BuildCv.Application.Features.Payments;

public sealed class HandleWebhookHandler(IPaymentStore store, IPaymentProvider provider)
{
    public async Task<Result<Payment>> HandleAsync(HandleWebhookCommand command, CancellationToken ct)
    {
        if (!provider.VerifyWebhookSignature(command.Payload, command.SignatureHeader))
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/INVALID_SIGNATURE", "Webhook signature verification failed"));
        }

        var wompiTransactionId = ExtractTransactionId(command.Payload);
        if (wompiTransactionId is null)
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/INVALID_PAYLOAD", "Could not extract transaction ID from payload"));
        }

        var payment = await store.GetByWompiTransactionIdAsync(wompiTransactionId, ct);
        if (payment is null)
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/NOT_FOUND", $"No payment found for transaction {wompiTransactionId}"));
        }

        if (payment.Status is PaymentStatus.Approved or PaymentStatus.Failed or PaymentStatus.Error)
        {
            return Result.Success(payment);
        }

        var newStatus = MapWompiStatus(command.Payload);
        var now = DateTime.UtcNow;

        Payment updated = newStatus switch
        {
            PaymentStatus.Approved => payment with
            {
                Status = PaymentStatus.Approved,
                PaidAt = now,
                UpdatedAt = now
            },
            PaymentStatus.Failed => payment with
            {
                Status = PaymentStatus.Failed,
                UpdatedAt = now
            },
            PaymentStatus.Error => payment with
            {
                Status = PaymentStatus.Error,
                UpdatedAt = now
            },
            _ => payment with { UpdatedAt = now }
        };

        await store.UpdateAsync(updated, ct);

        return Result.Success(updated);
    }

    private static string? ExtractTransactionId(string payload)
    {
        const string marker = "\"id\":";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + marker.Length;
        while (start < payload.Length && payload[start] == ' ')
        {
            start++;
        }

        if (start >= payload.Length)
        {
            return null;
        }

        if (payload[start] == '"')
        {
            start++;
            var end = payload.IndexOf('"', start);
            if (end < 0)
            {
                return null;
            }

            return payload[start..end];
        }

        var numEnd = start;
        while (numEnd < payload.Length && (char.IsDigit(payload[numEnd]) || payload[numEnd] == '-'))
        {
            numEnd++;
        }

        return start < numEnd ? payload[start..numEnd] : null;
    }

    private static PaymentStatus MapWompiStatus(string payload)
    {
        if (payload.Contains("\"APPROVED\"", StringComparison.Ordinal))
        {
            return PaymentStatus.Approved;
        }

        if (payload.Contains("\"DECLINED\"", StringComparison.Ordinal))
        {
            return PaymentStatus.Failed;
        }

        if (payload.Contains("\"ERROR\"", StringComparison.Ordinal))
        {
            return PaymentStatus.Error;
        }

        return PaymentStatus.Pending;
    }
}

public sealed record HandleWebhookCommand
{
    public string Payload { get; init; } = "";
    public string SignatureHeader { get; init; } = "";
}
