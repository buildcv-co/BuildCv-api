using BuildCv.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.PackageId).HasColumnName("package_id").HasMaxLength(20).IsRequired();
        builder.Property(p => p.Credits).HasColumnName("credits").IsRequired();
        builder.Property(p => p.AmountInCents).HasColumnName("amount_in_cents").IsRequired();
        builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasMaxLength(20).IsRequired()
            .HasConversion<string>();
        builder.Property(p => p.WompiTransactionId).HasColumnName("wompi_transaction_id").HasMaxLength(100);
        builder.Property(p => p.WompiPaymentLink).HasColumnName("wompi_payment_link").HasMaxLength(500);
        builder.Property(p => p.ProviderSessionId).HasColumnName("provider_session_id").HasMaxLength(200);
        builder.Property(p => p.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.PaidAt).HasColumnName("paid_at");

        builder.HasIndex(p => p.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_payments_idempotency_key");
        builder.HasIndex(p => p.WompiTransactionId)
            .IsUnique()
            .HasDatabaseName("UX_payments_wompi_transaction_id");
        builder.HasIndex(p => new { p.UserId, p.CreatedAt })
            .HasDatabaseName("IX_payments_user_id_created_at")
            .IsDescending(false, true);

        builder.ToTable(t => t.HasCheckConstraint("CK_payments_credits_positive", "credits > 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_payments_amount_positive", "amount_in_cents > 0"));
    }
}
