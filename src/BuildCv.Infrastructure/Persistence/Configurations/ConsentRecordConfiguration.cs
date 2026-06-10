using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("consent_records");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.PolicyVersion).HasColumnName("policy_version");
        builder.Property(c => c.ConsentDate).HasColumnName("consent_date");
        builder.Property(c => c.RevokedAt).HasColumnName("revoked_at");
        builder.Property(c => c.Purpose).HasColumnName("purpose").HasMaxLength(100);
        builder.HasIndex(c => new { c.UserId, c.Purpose });
        builder.Ignore(c => c.IsValid);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
