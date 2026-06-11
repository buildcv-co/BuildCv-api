using BuildCv.Application.Features.Invoicing;
using BuildCv.Domain.Common;
using BuildCv.Domain.Invoicing;
using BuildCv.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace BuildCv.Application.Features.Payments;

public sealed class PaymentReconciliationService(
    IPaymentStore store,
    IPaymentProvider provider,
    IInvoiceProvider? invoiceProvider,
    ILogger<PaymentReconciliationService> logger) : IPaymentReconciliationService
{
    public static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        var stale = await store.ListStalePendingAsync(StaleThreshold, ct);
        if (stale.Count == 0)
        {
            return 0;
        }

        var reconciled = 0;
        foreach (var payment in stale)
        {
            ct.ThrowIfCancellationRequested();
            if (await TryReconcileAsync(payment, ct))
            {
                reconciled++;
            }
        }

        return reconciled;
    }

    private async Task<bool> TryReconcileAsync(Payment payment, CancellationToken ct)
    {
        if (payment.WompiTransactionId is null)
        {
            return false;
        }

        try
        {
            var status = await provider.GetTransactionStatusAsync(payment.WompiTransactionId, ct);
            if (status is null)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var wompiStatus = MapWompiStatus(status.Status);

            Payment updated = wompiStatus switch
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
                _ => payment
            };

            if (updated.Status == payment.Status)
            {
                return false;
            }

            await store.UpdateAsync(updated, ct);

            if (updated.Status == PaymentStatus.Approved && invoiceProvider is not null)
            {
                await CreateInvoiceForPaymentAsync(updated, ct);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Payment reconciliation failed for transaction {TransactionId}",
                payment.WompiTransactionId);
            return false;
        }
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

        await invoiceProvider!.CreateInvoiceAsync(invoice, ct);
    }

    private static PaymentStatus MapWompiStatus(string status) => status switch
    {
        "APPROVED" => PaymentStatus.Approved,
        "DECLINED" => PaymentStatus.Failed,
        "ERROR" => PaymentStatus.Error,
        _ => PaymentStatus.Pending
    };
}
