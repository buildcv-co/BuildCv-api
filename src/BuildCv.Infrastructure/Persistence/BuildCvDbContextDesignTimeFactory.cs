using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BuildCv.Infrastructure.Persistence;

public sealed class BuildCvDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BuildCvDbContext>
{
    public BuildCvDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Postgres",
                ["Postgres:ConnectionString"] = "Host=localhost;Database=buildcv_design_time",
            })
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<BuildCvDbContext>();
        var settings = configuration.GetSection(PostgresSettings.SectionName).Get<PostgresSettings>()
            ?? new PostgresSettings();
        optionsBuilder.UseNpgsql(settings.ConnectionString);
        return new BuildCvDbContext(optionsBuilder.Options);
    }
}
