namespace CinemaSystem.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "POSTGRES_TEST_CONNECTION";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to an isolated PostgreSQL test database.";
        }
    }
}
