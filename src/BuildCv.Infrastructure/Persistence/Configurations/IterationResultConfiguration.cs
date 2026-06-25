using System.Text.Json;
using BuildCv.Domain.Iterations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class IterationResultConfiguration : IEntityTypeConfiguration<IterationResult>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public void Configure(EntityTypeBuilder<IterationResult> builder)
    {
        builder.ToTable("iteration_results");

        builder.HasKey(r => r.RequestId);
        builder.Property(r => r.RequestId).HasColumnName("request_id");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(r => r.BestStep)
            .HasColumnName("best_step")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOpts),
                v => string.IsNullOrEmpty(v)
                    ? null
                    : JsonSerializer.Deserialize<IterationStep>(v, JsonOpts));

        builder.Property(r => r.AllSteps)
            .HasColumnName("all_steps")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOpts),
                v => JsonSerializer.Deserialize<List<IterationStep>>(v, JsonOpts) ?? new List<IterationStep>());

        builder.Property(r => r.ProbabilityWarning).HasColumnName("probability_warning");
        builder.Property(r => r.CreditsConsumed).HasColumnName("credits_consumed");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at");

        builder.HasOne<IterationRequest>()
            .WithMany()
            .HasForeignKey(r => r.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.ExpiresAt)
            .HasDatabaseName("ix_iteration_results_expires_at");
    }
}
