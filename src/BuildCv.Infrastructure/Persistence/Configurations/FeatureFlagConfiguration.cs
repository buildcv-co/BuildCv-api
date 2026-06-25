using BuildCv.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags", t =>
            t.HasCheckConstraint("ck_feature_flags_current_value_not_null", "current_value IS NOT NULL"));
        builder.HasKey(f => f.Name);
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(f => f.DefaultValue).HasColumnName("default_value").IsRequired();
        builder.Property(f => f.CurrentValue).HasColumnName("current_value").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
