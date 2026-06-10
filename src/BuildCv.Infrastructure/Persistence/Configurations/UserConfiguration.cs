using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Provider).HasColumnName("provider").HasMaxLength(50);
        builder.Property(u => u.ProviderId).HasColumnName("provider_id").HasMaxLength(255);
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(255);
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.HasIndex(u => new { u.Provider, u.ProviderId }).IsUnique();
    }
}
