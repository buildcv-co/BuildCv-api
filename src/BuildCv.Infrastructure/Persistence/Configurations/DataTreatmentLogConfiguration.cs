using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class DataTreatmentLogConfiguration : IEntityTypeConfiguration<DataTreatmentLog>
{
    public void Configure(EntityTypeBuilder<DataTreatmentLog> builder)
    {
        builder.ToTable("data_treatment_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.UserId).HasColumnName("user_id");
        builder.Property(l => l.DataType).HasColumnName("data_type").HasMaxLength(50);
        builder.Property(l => l.Action).HasColumnName("action").HasMaxLength(50);
        builder.Property(l => l.Timestamp).HasColumnName("timestamp");
        builder.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.HasIndex(l => l.UserId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
