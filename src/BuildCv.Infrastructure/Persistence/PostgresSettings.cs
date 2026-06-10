namespace BuildCv.Infrastructure.Persistence;

public sealed class PostgresSettings
{
    public const string SectionName = "Postgres";

    public string ConnectionString { get; init; } = "";

    public bool EnableAutoMigrate { get; init; }
}
