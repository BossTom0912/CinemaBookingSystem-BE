using CinemaSystem.Infrastructure.Data;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CinemaSystem.Tests;

public sealed class PostgresMigrationIntegrationTests
{
    [PostgresFact]
    public async Task FreshSchema_MigratesAndEnforcesPostgresContracts()
    {
        await using var database = await PostgresTestSchema.CreateAsync();
        await database.MigrateAsync();

        Assert.Equal(51, await database.ScalarAsync<int>(
            """
            SELECT count(*)::integer
            FROM information_schema.tables
            WHERE table_schema = current_schema()
              AND table_type = 'BASE TABLE'
              AND table_name <> '__EFMigrationsHistory';
            """));
        Assert.Equal(4, await database.ScalarAsync<int>(
            "SELECT count(*)::integer FROM \"__EFMigrationsHistory\";"));
        Assert.Equal(70, await database.ScalarAsync<int>(
            """
            SELECT count(*)::integer
            FROM pg_catalog.pg_constraint constraint_meta
            JOIN pg_catalog.pg_class table_class
                ON table_class.oid = constraint_meta.conrelid
            JOIN pg_catalog.pg_namespace schema
                ON schema.oid = table_class.relnamespace
            WHERE schema.nspname = current_schema()
              AND constraint_meta.contype = 'c'
              AND constraint_meta.convalidated;
            """));
        Assert.Equal(10, await database.ScalarAsync<int>(
            """
            SELECT count(*)::integer
            FROM information_schema.triggers
            WHERE trigger_schema = current_schema()
              AND trigger_name LIKE 'TR_%_ROW_VERSION';
            """));

        var firstVersion = await database.ScalarAsync<long>(
            "SELECT nextval('cinema_row_version_seq');");
        var secondVersion = await database.ScalarAsync<long>(
            "SELECT nextval('cinema_row_version_seq');");
        Assert.NotEqual(firstVersion, secondVersion);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            """
            UPDATE "ROLE_PROVISIONING_POLICY"
            SET "requiresCinema" = false
            WHERE "roleId" = 'ROLE_STAFF';
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("CK_ROLE_PROVISIONING_POLICY_PROFILE_RULE", exception.ConstraintName);
    }

    [PostgresFact]
    public async Task ExistingMatchingSchema_IsAdoptedAndRemainsIdempotent()
    {
        await using var database = await PostgresTestSchema.CreateAsync();
        await database.MigrateAsync();
        await database.ExecuteAsync("DROP TABLE \"__EFMigrationsHistory\";");

        await database.MigrateAsync();

        Assert.Equal(4, await database.ScalarAsync<int>(
            "SELECT count(*)::integer FROM \"__EFMigrationsHistory\";"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT count(*)::integer
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20260726135020_InitialPostgresBaseline';
            """));
    }

    [PostgresFact]
    public async Task ExistingSchema_WithSameNamedWrongIndex_IsRejectedBeforeAdoption()
    {
        await using var database = await PostgresTestSchema.CreateAsync();
        await database.MigrateAsync();
        await database.ExecuteAsync(
            """
            DROP TABLE "__EFMigrationsHistory";
            DROP INDEX "IX_BOOKING_CHANNEL";
            CREATE INDEX "IX_BOOKING_CHANNEL" ON "BOOKING" ("bookingStatus");
            """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(database.MigrateAsync);

        Assert.Contains("Mismatched indexes", exception.Message, StringComparison.Ordinal);
        Assert.Contains("BOOKING.IX_BOOKING_CHANNEL", exception.Message, StringComparison.Ordinal);
        Assert.False(await database.ScalarAsync<bool>(
            "SELECT to_regclass('\"__EFMigrationsHistory\"') IS NOT NULL;"));
    }

    private sealed class PostgresTestSchema : IAsyncDisposable
    {
        private readonly string _adminConnectionString;

        private PostgresTestSchema(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            DatabaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string DatabaseName { get; }
        public string ConnectionString { get; }

        public static async Task<PostgresTestSchema> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(
                PostgresFactAttribute.ConnectionVariable)
                ?? throw new InvalidOperationException(
                    $"{PostgresFactAttribute.ConnectionVariable} is required.");
            var adminBuilder = new NpgsqlConnectionStringBuilder(configuredConnection)
            {
                Database = "postgres",
                SearchPath = null,
                Pooling = false
            };
            var databaseName = $"cinema_test_{Guid.NewGuid():N}";

            await using (var connection = new NpgsqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
                await command.ExecuteNonQueryAsync();
            }

            var testBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            return new PostgresTestSchema(
                adminBuilder.ConnectionString,
                databaseName,
                testBuilder.ConnectionString);
        }

        public async Task MigrateAsync()
        {
            await using var services = new ServiceCollection().BuildServiceProvider();
            await using var context = CreateContext();
            var maintenance = new DatabaseMaintenanceService(
                context,
                services,
                NullLogger<DatabaseMaintenanceService>.Instance);
            await maintenance.MigrateAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (T)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("The PostgreSQL query returned null."));
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }

        private CinemaDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            return new CinemaDbContext(options);
        }
    }
}
