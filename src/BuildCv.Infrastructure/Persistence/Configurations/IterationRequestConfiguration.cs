using BuildCv.Domain.Iterations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class IterationRequestConfiguration : IEntityTypeConfiguration<IterationRequest>
{
    public void Configure(EntityTypeBuilder<IterationRequest> builder)
    {
        builder.ToTable("iteration_requests");

        builder.HasKey(r => r.RequestId);
        builder.Property(r => r.RequestId).HasColumnName("request_id");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.CvText).HasColumnName("cv_text").HasColumnType("text").IsRequired();
        builder.Property(r => r.VacancyText).HasColumnName("vacancy_text").HasColumnType("text").IsRequired();
        builder.Property(r => r.IterationCount).HasColumnName("iteration_count");
        builder.Property(r => r.ProbabilityThreshold).HasColumnName("probability_threshold");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.HasOne<BuildCv.Domain.Auth.User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.UserId, r.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_iteration_requests_user_created_at");

        builder.HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("ix_iteration_requests_status_created_at");

        builder.Property<uint>("xmin")
            .IsRowVersion();
    }
}
