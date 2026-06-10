using BuildCv.Domain.Invoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence;

public sealed class NumberingRangeConfiguration : IEntityTypeConfiguration<NumberingRange>
{
    public void Configure(EntityTypeBuilder<NumberingRange> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Prefix).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.ProviderId);
    }
}
