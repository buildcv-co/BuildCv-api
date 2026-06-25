using BuildCv.Domain.Auth;
using BuildCv.Domain.Credits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class CreditLedgerEntryConfiguration : IEntityTypeConfiguration<CreditLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CreditLedgerEntry> builder)
    {
        builder.ToTable("credit_ledger_entries", t =>
        {
            t.HasCheckConstraint("ck_credit_ledger_delta_nonzero", "delta <> 0");
            t.HasCheckConstraint("ck_credit_ledger_balance_nonneg", "balance_after >= 0");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(e => e.Reference).HasColumnName("reference").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Delta).HasColumnName("delta").IsRequired();
        builder.Property(e => e.BalanceAfter).HasColumnName("balance_after").IsRequired();
        builder.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.Reason, e.Reference })
            .IsUnique()
            .HasDatabaseName("ux_credit_ledger_user_reason_reference");

        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_credit_ledger_user_created_at");
    }
}
