using BuildCv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BuildCv.Infrastructure.Tests.Credits;

/// <summary>
/// xUnit collection that provisions a real PostgreSQL 16 instance via
/// the dev/test container started outside the test runner. All tests
/// in this collection share the same connection string and run
/// sequentially so the EF migration is applied once.
/// </summary>
[CollectionDefinition("PostgresCredits")]
public sealed class PostgresCreditsCollection : ICollectionFixture<PostgresCreditsFixture>
{
}

public sealed class PostgresCreditsFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Host=127.0.0.1;Port=5435;Database=buildcv_credits_test;Username=postgres;Password=postgres";

    public string AdminConnectionString { get; } =
        "Host=127.0.0.1;Port=5435;Database=postgres;Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using (var dropCmd = admin.CreateCommand())
        {
            dropCmd.CommandText = "DROP DATABASE IF EXISTS buildcv_credits_test WITH (FORCE);";
            await dropCmd.ExecuteNonQueryAsync();
        }
        await using (var createCmd = admin.CreateCommand())
        {
            createCmd.CommandText = "CREATE DATABASE buildcv_credits_test;";
            await createCmd.ExecuteNonQueryAsync();
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        var options = new DbContextOptionsBuilder<BuildCvDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new BuildCvDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using var dropCmd = admin.CreateCommand();
        dropCmd.CommandText = "DROP DATABASE IF EXISTS buildcv_credits_test WITH (FORCE);";
        await dropCmd.ExecuteNonQueryAsync();
    }
}
