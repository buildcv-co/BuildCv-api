using BuildCv.Domain.Auth;
using BuildCv.Domain.Credits;
using BuildCv.Domain.Invoicing;
using BuildCv.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Persistence;

public sealed class BuildCvDbContext(DbContextOptions<BuildCvDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    public DbSet<DataTreatmentLog> DataTreatmentLogs => Set<DataTreatmentLog>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<NumberingRange> NumberingRanges => Set<NumberingRange>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuildCvDbContext).Assembly);
    }
}
