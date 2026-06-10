using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Token);
        builder.Property(t => t.Token).HasColumnName("token").HasMaxLength(200);
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.RevokedAt).HasColumnName("revoked_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(t => t.UserId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
