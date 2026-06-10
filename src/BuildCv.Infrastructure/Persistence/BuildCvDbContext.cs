using BuildCv.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace BuildCv.Infrastructure.Persistence;

public sealed class BuildCvDbContext(DbContextOptions<BuildCvDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    public DbSet<DataTreatmentLog> DataTreatmentLogs => Set<DataTreatmentLog>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuildCvDbContext).Assembly);
    }
}
