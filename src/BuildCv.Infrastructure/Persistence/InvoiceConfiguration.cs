using BuildCv.Domain.Invoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildCv.Infrastructure.Persistence;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ReferenceCode).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Number).HasMaxLength(50);
        builder.Property(i => i.Cufe).HasMaxLength(100);
        builder.Property(i => i.Uuid).HasMaxLength(100);
        builder.Property(i => i.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Currency).HasMaxLength(3).IsRequired();
        builder.Property(i => i.Status).HasMaxLength(50).IsRequired();
        builder.Property(i => i.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.CustomerIdentification).HasMaxLength(50).IsRequired();
        builder.Property(i => i.CustomerEmail).HasMaxLength(200).IsRequired();
        builder.Property(i => i.CustomerPhone).HasMaxLength(50);
        builder.Property(i => i.CustomerAddress).HasMaxLength(500);
        builder.Property(i => i.CustomerCompany).HasMaxLength(200);
        builder.Property(i => i.CustomerTradeName).HasMaxLength(200);
        builder.Property(i => i.CustomerLegalOrganizationCode).HasMaxLength(10);
        builder.Property(i => i.CustomerTributeCode).HasMaxLength(10);
        builder.Property(i => i.CustomerMunicipalityCode).HasMaxLength(10);
        builder.Property(i => i.CustomerIdentificationDocumentCode).HasMaxLength(10);
        builder.Property(i => i.ItemsDescription).HasMaxLength(1000);
        builder.Property(i => i.PaymentMethodCode).HasMaxLength(10);
        builder.Property(i => i.FactusResponseJson).HasMaxLength(4000);
        builder.Property(i => i.ErrorJson).HasMaxLength(4000);
        builder.HasIndex(i => i.ReferenceCode).IsUnique();
        builder.HasIndex(i => i.Number);
        builder.HasIndex(i => i.UserId);
    }
}
