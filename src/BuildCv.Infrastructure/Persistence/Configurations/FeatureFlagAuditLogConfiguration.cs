using BuildCv.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagAuditLogConfiguration : IEntityTypeConfiguration<FeatureFlagAuditLog>
{
    public void Configure(EntityTypeBuilder<FeatureFlagAuditLog> builder)
    {
        builder.ToTable("feature_flag_audit_log", t =>
            t.HasCheckConstraint("ck_feature_flag_audit_log_new_value_not_null", "new_value IS NOT NULL"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.FlagName).HasColumnName("flag_name").HasMaxLength(100).IsRequired();
        builder.Property(l => l.OldValue).HasColumnName("old_value");
        builder.Property(l => l.NewValue).HasColumnName("new_value").IsRequired();
        builder.Property(l => l.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(l => l.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(500);

        builder.HasIndex(l => new { l.FlagName, l.ChangedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_feature_flag_audit_log_flag_name_changed_at");
    }
}
