using System.Text.Json;
using BuildCv.Application.Common;
using BuildCv.Application.Features.Credits;
using BuildCv.Application.Features.Invoicing;
using BuildCv.Application.Features.Subscriptions;
using BuildCv.Domain.Common;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Invoicing;
using BuildCv.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Payments;

public sealed class HandleWebhookHandler
{
    private readonly IPaymentStore _store;
    private readonly IPaymentProvider _provider;
    private readonly IInvoiceProvider? _invoiceProvider;
    private readonly ICreditLedger? _creditLedger;
    private readonly ICreditsFeatureFlag _creditsFeature;
    private readonly HandleRecurringChargeHandler? _recurringHandler;
    private readonly ILogger<HandleWebhookHandler> _logger;

    public HandleWebhookHandler(
        IPaymentStore store,
        IPaymentProvider provider,
        IInvoiceProvider? invoiceProvider,
        ICreditLedger? creditLedger,
        ICreditsFeatureFlag creditsFeature,
        ILogger<HandleWebhookHandler> logger)
        : this(store, provider, invoiceProvider, creditLedger, creditsFeature, recurringHandler: null, logger)
    {
    }

    public HandleWebhookHandler(
        IPaymentStore store,
        IPaymentProvider provider,
        IInvoiceProvider? invoiceProvider,
        ICreditLedger? creditLedger,
        ICreditsFeatureFlag creditsFeature,
        HandleRecurringChargeHandler? recurringHandler,
        ILogger<HandleWebhookHandler> logger)
    {
        _store = store;
        _provider = provider;
        _invoiceProvider = invoiceProvider;
        _creditLedger = creditLedger;
        _creditsFeature = creditsFeature;
        _recurringHandler = recurringHandler;
        _logger = logger;
    }

    public async Task<Result<Payment>> HandleAsync(HandleWebhookCommand command, CancellationToken ct = default)
    {
        if (!_provider.VerifyWebhookSignature(command.Payload, command.SignatureHeader))
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/INVALID_SIGNATURE", "Webhook signature verification failed"));
        }

        var eventType = ExtractEventType(command.Payload);
        if (eventType is "recurring_charge.successful" or "recurring_charge.failed")
        {
            return await HandleSubscriptionEventAsync(eventType, command, ct);
        }

        var wompiTransactionId = ExtractTransactionId(command.Payload);
        if (wompiTransactionId is null)
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/INVALID_PAYLOAD", "Could not extract transaction ID from payload"));
        }

        var payment = await _store.GetByWompiTransactionIdAsync(wompiTransactionId, ct);
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

        await _store.UpdateAsync(updated, ct);

        if (updated.Status == PaymentStatus.Approved)
        {
            if (_invoiceProvider is not null)
            {
                try
                {
                    await CreateInvoiceForPaymentAsync(updated, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Invoice creation failed for payment {PaymentId}; payment remains Approved",
                        updated.Id);
                }
            }

            if (_creditsFeature.IsEnabled && _creditLedger is not null)
            {
                try
                {
                    await AccreditCreditsAsync(updated, _creditLedger, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Credit grant failed for payment {PaymentId}; payment remains Approved (reconciliation will retry)",
                        updated.Id);
                }
            }
        }

        return Result.Success(updated);
    }

    private async Task<Result<Payment>> HandleSubscriptionEventAsync(
        string eventType,
        HandleWebhookCommand command,
        CancellationToken ct)
    {
        if (_recurringHandler is null)
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/UNSUPPORTED_EVENT",
                    $"Subscription event '{eventType}' received but subscription handler is not registered"));
        }

        var paymentSourceId = ExtractPaymentSourceId(command.Payload);
        if (paymentSourceId is null)
        {
            return Result.Failure<Payment>(
                new Error("PAYMENT/INVALID_PAYLOAD", "Could not extract payment_source_id from payload"));
        }

        var chargeId = ExtractChargeId(command.Payload) ?? paymentSourceId;
        var now = DateTime.UtcNow;

        if (eventType == "recurring_charge.successful")
        {
            await _recurringHandler.HandleSuccessAsync(paymentSourceId, now, chargeId, ct);
            _logger.LogInformation("Recurring charge {ChargeId} processed via webhook", chargeId);
        }
        else
        {
            var reason = ExtractFailureReason(command.Payload) ?? "unknown";
            await _recurringHandler.HandleFailureAsync(paymentSourceId, now, reason, ct);
            _logger.LogInformation("Recurring charge {ChargeId} failure processed via webhook", chargeId);
        }

        return Result.Success<Payment>(default!);
    }

    private async Task AccreditCreditsAsync(Payment payment, ICreditLedger ledger, CancellationToken ct)
    {
        var balance = await ledger.GetBalanceAsync(payment.UserId, ct);
        var newBalance = balance + payment.Credits;

        await ledger.AccreditAsync(
            userId: payment.UserId,
            reason: CreditLedgerReason.Purchase,
            reference: $"payment:{payment.Id}",
            delta: payment.Credits,
            balanceAfter: newBalance,
            metadata: JsonSerializer.Serialize(new { payment.Id, payment.WompiTransactionId }),
            ct: ct);

        _logger.LogInformation(
            "Credit grant for payment {PaymentId}: user {UserId} +{Credits} (balance {NewBalance})",
            payment.Id, payment.UserId, payment.Credits, newBalance);
    }

    private async Task CreateInvoiceForPaymentAsync(Payment payment, CancellationToken ct)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = payment.UserId,
            DocumentType = InvoiceType.Invoice,
            ReferenceCode = payment.Id.ToString(),
            AmountInCents = payment.AmountInCents,
            Currency = payment.Currency,
            Status = InvoiceStatus.Draft,
            CustomerName = "BuildCV Customer",
            CustomerIdentification = "2222222222",
            CustomerEmail = "no-reply@buildcv.com",
            CustomerPhone = "+57 300 000 0000",
            CustomerAddress = "Digital",
            CustomerCompany = "BuildCV Customer",
            CustomerLegalOrganizationCode = "2",
            CustomerTributeCode = "ZZ",
            CustomerMunicipalityCode = "11001",
            CustomerIdentificationDocumentCode = "13",
            ItemsJson = "[]",
            ItemsDescription = $"{payment.Credits} BuildCV credits",
            PaymentDetailsJson = "[]",
            PaymentMethodCode = "10",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _invoiceProvider!.CreateInvoiceAsync(invoice, ct);
        _logger.LogInformation("Invoice created for payment {PaymentId}", payment.Id);
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

    private static string? ExtractEventType(string payload)
    {
        const string marker = "\"event\":\"";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + marker.Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? null : payload[start..end];
    }

    private static string? ExtractPaymentSourceId(string payload)
    {
        const string marker = "\"payment_source_id\":\"";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + marker.Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? null : payload[start..end];
    }

    private static string? ExtractChargeId(string payload)
    {
        const string marker = "\"charge_id\":\"";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + marker.Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? null : payload[start..end];
    }

    private static string? ExtractFailureReason(string payload)
    {
        const string marker = "\"reason\":\"";
        var idx = payload.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + marker.Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? null : payload[start..end];
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
