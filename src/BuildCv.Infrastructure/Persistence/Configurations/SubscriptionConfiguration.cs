using BuildCv.Domain.Auth;
using BuildCv.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions", t =>
        {
            t.HasCheckConstraint("ck_subscriptions_status", "status IN (1,2,3)");
            t.HasCheckConstraint("ck_subscriptions_plan", "plan IN (1,2)");
            t.HasCheckConstraint("ck_subscriptions_retry_count", "retry_count >= 0 AND retry_count <= 3");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.Plan)
            .HasColumnName("plan")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(s => s.PaymentSourceId)
            .HasColumnName("payment_source_id")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(s => s.StartedAt).HasColumnName("started_at");
        builder.Property(s => s.CurrentPeriodStart).HasColumnName("current_period_start");
        builder.Property(s => s.CurrentPeriodEnd).HasColumnName("current_period_end");
        builder.Property(s => s.CanceledAt).HasColumnName("canceled_at");
        builder.Property(s => s.LastChargeAt).HasColumnName("last_charge_at");
        builder.Property(s => s.NextChargeAt).HasColumnName("next_charge_at");
        builder.Property(s => s.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("status != 3")
            .HasDatabaseName("ux_subscriptions_user_active");

        builder.HasIndex(s => new { s.Status, s.NextChargeAt })
            .HasDatabaseName("ix_subscriptions_status_next_charge");

        builder.Property<uint>("xmin")
            .IsRowVersion();
    }
}
