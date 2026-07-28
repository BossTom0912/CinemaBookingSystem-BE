// Khai báo các interface được định nghĩa trong tầng Application
using CinemaSystem.Application.Interfaces;
// Sử dụng các lớp và cấu hình liên quan đến truy xuất dữ liệu trong tầng Infrastructure
using CinemaSystem.Infrastructure.Persistence;
// Sử dụng Entity Framework Core để thao tác và quản lý cơ sở dữ liệu
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System.Data;

// Khai báo namespace quản lý dữ liệu (Data) trong tầng Infrastructure
namespace CinemaSystem.Infrastructure.Data;

// Định nghĩa lớp DatabaseMaintenanceService triển khai interface IDatabaseMaintenanceService, đánh dấu sealed để ngăn kế thừa
public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private const string PostgresBaselineMigrationId = "20260726135020_InitialPostgresBaseline";
    private static readonly string EfProductVersion =
        typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "8.0.0";
    private static readonly HashSet<(string Table, string Column)> LegacyColumnsAddedAfterBaseline =
    [
        ("VOUCHER", "roomId"),
        ("VOUCHER", "showtimeId")
    ];
    private static readonly HashSet<(string Table, string Column)> LegacyRowVersionColumns =
    [
        ("SHOWTIME_SEAT", "rowVersion"),
        ("REFUND_CLAIM", "rowVersion"),
        ("MANUAL_REFUND_PROCESS", "rowVersion"),
        ("COMPENSATION_TICKET", "rowVersion"),
        ("COMPENSATION_COMBO", "rowVersion")
    ];

    private sealed record ExistingColumn(string StoreType, bool IsNullable, bool HasDefault);
    private sealed record ExistingIndex(bool IsUnique, string[] Columns, string? Filter);
    private sealed record ExistingForeignKey(
        string[] Columns,
        string PrincipalTable,
        string[] PrincipalColumns,
        char DeleteAction);
    private sealed record ExpectedColumn(
        string Table,
        string Column,
        string StoreType,
        bool IsNullable,
        bool HasDefault);
    private sealed record ExpectedIndex(
        string Table,
        string Name,
        bool IsUnique,
        string[] Columns,
        string? Filter);
    private sealed record ExpectedForeignKey(
        string Table,
        string Name,
        string[] Columns,
        string PrincipalTable,
        string[] PrincipalColumns,
        char DeleteAction);

    // Biến private chỉ đọc để giữ kết nối với cơ sở dữ liệu qua CinemaDbContext
    private readonly CinemaDbContext _dbContext;
    // Biến private chỉ đọc để giữ tham chiếu tới IServiceProvider nhằm resolve các dependency khi cần
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMaintenanceService> _logger;

    // Constructor khởi tạo DatabaseMaintenanceService với các dependency được tiêm vào (DI)
    public DatabaseMaintenanceService(
        CinemaDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<DatabaseMaintenanceService> logger)
    {
        // Gán dbContext được tiêm vào biến _dbContext
        _dbContext = dbContext;
        // Gán serviceProvider được tiêm vào biến _serviceProvider
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Phương thức bất đồng bộ để thực thi các file Migration lên cơ sở dữ liệu
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await AdoptExistingPostgresSchemaAsync(cancellationToken);

        // Gọi phương thức MigrateAsync của EF Core để tự động cập nhật Database schema lên phiên bản mới nhất
        await _dbContext.Database.MigrateAsync(cancellationToken);
        await ValidatePostgresCheckConstraintsAsync(cancellationToken);
    }

    private async Task AdoptExistingPostgresSchemaAsync(CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            return;
        }

        var connection = _dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var existingColumns = new Dictionary<(string Table, string Column), ExistingColumn>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT
                        table_name,
                        column_name,
                        format_type(a.atttypid, a.atttypmod) AS store_type,
                        is_nullable = 'YES' AS is_nullable,
                        column_default IS NOT NULL AS has_default
                    FROM information_schema.columns c
                    JOIN pg_catalog.pg_namespace n
                        ON n.nspname = c.table_schema
                    JOIN pg_catalog.pg_class t
                        ON t.relnamespace = n.oid
                       AND t.relname = c.table_name
                    JOIN pg_catalog.pg_attribute a
                        ON a.attrelid = t.oid
                       AND a.attname = c.column_name
                    WHERE table_schema = current_schema();
                    """;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingColumns[(reader.GetString(0), reader.GetString(1))] =
                        new ExistingColumn(
                            reader.GetString(2),
                            reader.GetBoolean(3),
                            reader.GetBoolean(4));
                }
            }

            var hasApplicationTables = existingColumns.Keys.Any(
                column => column.Table != "__EFMigrationsHistory");
            if (!hasApplicationTables)
            {
                return;
            }

            var baselineAlreadyApplied = false;
            if (existingColumns.ContainsKey(("__EFMigrationsHistory", "MigrationId")))
            {
                await using var historyCommand = connection.CreateCommand();
                historyCommand.CommandText =
                    """
                    SELECT EXISTS (
                        SELECT 1
                        FROM "__EFMigrationsHistory"
                        WHERE "MigrationId" = @migration_id);
                    """;
                var migrationParameter = historyCommand.CreateParameter();
                migrationParameter.ParameterName = "migration_id";
                migrationParameter.Value = PostgresBaselineMigrationId;
                historyCommand.Parameters.Add(migrationParameter);
                baselineAlreadyApplied = (bool)(
                    await historyCommand.ExecuteScalarAsync(cancellationToken) ?? false);
            }

            if (baselineAlreadyApplied)
            {
                return;
            }

            var existingIndexes = new Dictionary<(string Table, string Name), ExistingIndex>();
            await using (var indexCommand = connection.CreateCommand())
            {
                indexCommand.CommandText =
                    """
                    SELECT
                        table_class.relname AS table_name,
                        index_class.relname AS index_name,
                        index_meta.indisunique,
                        ARRAY(
                            SELECT attribute.attname
                            FROM unnest(index_meta.indkey) WITH ORDINALITY AS key(attnum, ordinal)
                            JOIN pg_catalog.pg_attribute attribute
                                ON attribute.attrelid = table_class.oid
                               AND attribute.attnum = key.attnum
                            ORDER BY key.ordinal
                        ) AS column_names,
                        pg_get_expr(index_meta.indpred, index_meta.indrelid) AS filter
                    FROM pg_catalog.pg_index index_meta
                    JOIN pg_catalog.pg_class table_class
                        ON table_class.oid = index_meta.indrelid
                    JOIN pg_catalog.pg_class index_class
                        ON index_class.oid = index_meta.indexrelid
                    JOIN pg_catalog.pg_namespace schema
                        ON schema.oid = table_class.relnamespace
                    WHERE schema.nspname = current_schema();
                    """;

                await using var reader = await indexCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingIndexes[(reader.GetString(0), reader.GetString(1))] =
                        new ExistingIndex(
                            reader.GetBoolean(2),
                            reader.GetFieldValue<string[]>(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4));
                }
            }

            var existingConstraints = new HashSet<(string Table, string Name)>();
            await using (var constraintCommand = connection.CreateCommand())
            {
                constraintCommand.CommandText =
                    """
                    SELECT table_name, constraint_name
                    FROM information_schema.table_constraints
                    WHERE constraint_schema = current_schema();
                    """;

                await using var reader = await constraintCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingConstraints.Add((reader.GetString(0), reader.GetString(1)));
                }
            }

            var existingForeignKeys = new Dictionary<(string Table, string Name), ExistingForeignKey>();
            await using (var foreignKeyCommand = connection.CreateCommand())
            {
                foreignKeyCommand.CommandText =
                    """
                    SELECT
                        dependent_table.relname AS table_name,
                        constraint_meta.conname AS constraint_name,
                        ARRAY(
                            SELECT attribute.attname
                            FROM unnest(constraint_meta.conkey) WITH ORDINALITY AS key(attnum, ordinal)
                            JOIN pg_catalog.pg_attribute attribute
                                ON attribute.attrelid = dependent_table.oid
                               AND attribute.attnum = key.attnum
                            ORDER BY key.ordinal
                        ) AS column_names,
                        principal_table.relname AS principal_table_name,
                        ARRAY(
                            SELECT attribute.attname
                            FROM unnest(constraint_meta.confkey) WITH ORDINALITY AS key(attnum, ordinal)
                            JOIN pg_catalog.pg_attribute attribute
                                ON attribute.attrelid = principal_table.oid
                               AND attribute.attnum = key.attnum
                            ORDER BY key.ordinal
                        ) AS principal_column_names,
                        constraint_meta.confdeltype
                    FROM pg_catalog.pg_constraint constraint_meta
                    JOIN pg_catalog.pg_class dependent_table
                        ON dependent_table.oid = constraint_meta.conrelid
                    JOIN pg_catalog.pg_class principal_table
                        ON principal_table.oid = constraint_meta.confrelid
                    JOIN pg_catalog.pg_namespace schema
                        ON schema.oid = dependent_table.relnamespace
                    WHERE schema.nspname = current_schema()
                      AND constraint_meta.contype = 'f';
                    """;

                await using var reader = await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingForeignKeys[(reader.GetString(0), reader.GetString(1))] =
                        new ExistingForeignKey(
                            reader.GetFieldValue<string[]>(2),
                            reader.GetString(3),
                            reader.GetFieldValue<string[]>(4),
                            reader.GetChar(5));
                }
            }

            var mappedEntityTypes = _dbContext.Model
                .GetEntityTypes()
                .Where(entityType => entityType.GetTableName() is not null)
                .ToArray();

            var expectedColumns = mappedEntityTypes
                .SelectMany(entityType =>
                {
                    var tableName = entityType.GetTableName();
                    if (tableName is null)
                    {
                        return Array.Empty<ExpectedColumn>();
                    }

                    var table = StoreObjectIdentifier.Table(
                        tableName,
                        entityType.GetSchema());
                    return entityType.GetProperties()
                        .Select(property => new ExpectedColumn(
                            tableName,
                            property.GetColumnName(table)!,
                            property.GetColumnType() ?? property.GetRelationalTypeMapping().StoreType,
                            property.IsNullable,
                            property.FindAnnotation(RelationalAnnotationNames.DefaultValue) is not null ||
                                property.FindAnnotation(RelationalAnnotationNames.DefaultValueSql) is not null ||
                                property.ValueGenerated == ValueGenerated.OnAddOrUpdate))
                        .Where(column => column.Column is not null);
                })
                .Distinct()
                .ToArray();

            var expectedIndexes = mappedEntityTypes
                .SelectMany(entityType =>
                {
                    var tableName = entityType.GetTableName()!;
                    var table = StoreObjectIdentifier.Table(
                        tableName,
                        entityType.GetSchema());
                    return entityType.GetIndexes()
                        .Select(index => new ExpectedIndex(
                            tableName,
                            index.GetDatabaseName()!,
                            index.IsUnique,
                            index.Properties
                                .Select(property => property.GetColumnName(table)!)
                                .ToArray(),
                            index.GetFilter()))
                        .Where(index => index.Name is not null);
                })
                .Distinct()
                .ToArray();

            var expectedForeignKeys = mappedEntityTypes
                .SelectMany(entityType =>
                {
                    var tableName = entityType.GetTableName()!;
                    var table = StoreObjectIdentifier.Table(
                        tableName,
                        entityType.GetSchema());
                    return entityType.GetForeignKeys()
                        .Select(foreignKey =>
                        {
                            var principalEntityType = foreignKey.PrincipalEntityType;
                            var principalTableName = principalEntityType.GetTableName()!;
                            var principalTable = StoreObjectIdentifier.Table(
                                principalTableName,
                                principalEntityType.GetSchema());
                            return new ExpectedForeignKey(
                                tableName,
                                foreignKey.GetConstraintName()!,
                                foreignKey.Properties
                                    .Select(property => property.GetColumnName(table)!)
                                    .ToArray(),
                                principalTableName,
                                foreignKey.PrincipalKey.Properties
                                    .Select(property => property.GetColumnName(principalTable)!)
                                    .ToArray(),
                                GetPostgresDeleteAction(foreignKey.DeleteBehavior));
                        })
                        .Where(foreignKey => foreignKey.Name is not null);
                })
                .Distinct()
                .ToArray();

            var expectedConstraints = mappedEntityTypes
                .SelectMany(entityType =>
                {
                    var tableName = entityType.GetTableName()!;
                    var keyNames = entityType.GetKeys()
                        .Select(key => key.GetName());
                    var foreignKeyNames = entityType.GetForeignKeys()
                        .Select(foreignKey => foreignKey.GetConstraintName());

                    return keyNames
                        .Concat(foreignKeyNames)
                        .Where(constraintName => constraintName is not null)
                        .Select(constraintName => (Table: tableName, Name: constraintName!));
                })
                .Distinct()
                .ToArray();

            var missingColumns = expectedColumns
                .Where(expected =>
                    !existingColumns.ContainsKey((expected.Table, expected.Column)) &&
                    !LegacyColumnsAddedAfterBaseline.Contains((expected.Table, expected.Column)))
                .OrderBy(expected => expected.Table, StringComparer.Ordinal)
                .ThenBy(expected => expected.Column, StringComparer.Ordinal)
                .ToArray();

            var mismatchedColumns = expectedColumns
                .Where(expected => existingColumns.TryGetValue(
                    (expected.Table, expected.Column),
                    out var actual) &&
                    IsUnexpectedLegacyColumnMismatch(expected, actual))
                .OrderBy(expected => expected.Table, StringComparer.Ordinal)
                .ThenBy(expected => expected.Column, StringComparer.Ordinal)
                .ToArray();

            var missingIndexes = expectedIndexes
                .Where(expected => !existingIndexes.ContainsKey((expected.Table, expected.Name)))
                .OrderBy(expected => expected.Table, StringComparer.Ordinal)
                .ThenBy(expected => expected.Name, StringComparer.Ordinal)
                .ToArray();

            var mismatchedIndexes = expectedIndexes
                .Where(expected => existingIndexes.TryGetValue(
                    (expected.Table, expected.Name),
                    out var actual) &&
                    (actual.IsUnique != expected.IsUnique ||
                     !actual.Columns.SequenceEqual(expected.Columns, StringComparer.Ordinal) ||
                     NormalizeSqlFragment(actual.Filter) != NormalizeSqlFragment(expected.Filter)))
                .OrderBy(expected => expected.Table, StringComparer.Ordinal)
                .ThenBy(expected => expected.Name, StringComparer.Ordinal)
                .ToArray();

            var missingConstraints = expectedConstraints
                .Where(expected => !existingConstraints.Contains(expected))
                .OrderBy(expected => expected.Table, StringComparer.Ordinal)
                .ThenBy(expected => expected.Name, StringComparer.Ordinal)
                .ToArray();

            var mismatchedForeignKeys = expectedForeignKeys
                .Where(expected => existingForeignKeys.TryGetValue(
                    (expected.Table, expected.Name),
                    out var actual) &&
                    (!actual.Columns.SequenceEqual(expected.Columns, StringComparer.Ordinal) ||
                     actual.PrincipalTable != expected.PrincipalTable ||
                     !actual.PrincipalColumns.SequenceEqual(expected.PrincipalColumns, StringComparer.Ordinal) ||
                     actual.DeleteAction != expected.DeleteAction))
                .OrderBy(expected => expected.Table, StringComparer.Ordinal)
                .ThenBy(expected => expected.Name, StringComparer.Ordinal)
                .ToArray();

            if (missingColumns.Length > 0 ||
                mismatchedColumns.Length > 0 ||
                missingIndexes.Length > 0 ||
                mismatchedIndexes.Length > 0 ||
                missingConstraints.Length > 0 ||
                mismatchedForeignKeys.Length > 0)
            {
                var missingColumnSample = string.Join(
                    ", ",
                    missingColumns.Take(20).Select(item => $"{item.Table}.{item.Column}"));
                var missingIndexSample = string.Join(
                    ", ",
                    missingIndexes.Take(20).Select(item => $"{item.Table}.{item.Name}"));
                var missingConstraintSample = string.Join(
                    ", ",
                    missingConstraints.Take(20).Select(item => $"{item.Table}.{item.Name}"));
                var mismatchedColumnSample = string.Join(
                    ", ",
                    mismatchedColumns.Take(20).Select(item => $"{item.Table}.{item.Column}"));
                var mismatchedIndexSample = string.Join(
                    ", ",
                    mismatchedIndexes.Take(20).Select(item => $"{item.Table}.{item.Name}"));
                var mismatchedForeignKeySample = string.Join(
                    ", ",
                    mismatchedForeignKeys.Take(20).Select(item => $"{item.Table}.{item.Name}"));
                throw new InvalidOperationException(
                    $"The existing PostgreSQL database cannot adopt baseline '{PostgresBaselineMigrationId}' " +
                    "because its mapped schema is incomplete. " +
                    $"Missing columns ({missingColumns.Length}): {missingColumnSample}. " +
                    $"Mismatched columns ({mismatchedColumns.Length}): {mismatchedColumnSample}. " +
                    $"Missing indexes ({missingIndexes.Length}): {missingIndexSample}. " +
                    $"Mismatched indexes ({mismatchedIndexes.Length}): {mismatchedIndexSample}. " +
                    $"Missing constraints ({missingConstraints.Length}): {missingConstraintSample}. " +
                    $"Mismatched foreign keys ({mismatchedForeignKeys.Length}): {mismatchedForeignKeySample}. " +
                    "Restore a production clone and reconcile its schema before deployment.");
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var createHistoryCommand = connection.CreateCommand())
            {
                createHistoryCommand.Transaction = transaction;
                createHistoryCommand.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" character varying(150) NOT NULL,
                        "ProductVersion" character varying(32) NOT NULL,
                        CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                    );
                    """;
                await createHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insertHistoryCommand = connection.CreateCommand())
            {
                insertHistoryCommand.Transaction = transaction;
                insertHistoryCommand.CommandText =
                    """
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES (@migration_id, @product_version)
                    ON CONFLICT ("MigrationId") DO NOTHING;
                    """;

                var migrationParameter = insertHistoryCommand.CreateParameter();
                migrationParameter.ParameterName = "migration_id";
                migrationParameter.Value = PostgresBaselineMigrationId;
                insertHistoryCommand.Parameters.Add(migrationParameter);

                var versionParameter = insertHistoryCommand.CreateParameter();
                versionParameter.ParameterName = "product_version";
                versionParameter.Value = EfProductVersion;
                insertHistoryCommand.Parameters.Add(versionParameter);

                await insertHistoryCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogWarning(
                "Adopted verified existing PostgreSQL schema as EF baseline {MigrationId}.",
                PostgresBaselineMigrationId);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    // Phương thức bất đồng bộ để tạo dữ liệu mẫu (Seed data) vào cơ sở dữ liệu ban đầu
    private async Task ValidatePostgresCheckConstraintsAsync(CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            return;
        }

        var expectedConstraints = _dbContext.GetService<IDesignTimeModel>().Model
            .GetEntityTypes()
            .Where(entityType => entityType.GetTableName() is not null)
            .SelectMany(entityType => entityType.GetCheckConstraints()
                .Select(constraint => (
                    Table: entityType.GetTableName()!,
                    Name: constraint.Name)))
            .Distinct()
            .ToArray();

        if (expectedConstraints.Length == 0)
        {
            throw new InvalidOperationException(
                "The PostgreSQL EF model does not define any business check constraints.");
        }

        var connection = _dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var validatedConstraints = new HashSet<(string Table, string Name)>();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT table_class.relname, constraint_meta.conname
                FROM pg_catalog.pg_constraint constraint_meta
                JOIN pg_catalog.pg_class table_class
                    ON table_class.oid = constraint_meta.conrelid
                JOIN pg_catalog.pg_namespace schema
                    ON schema.oid = table_class.relnamespace
                WHERE schema.nspname = current_schema()
                  AND constraint_meta.contype = 'c'
                  AND constraint_meta.convalidated;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                validatedConstraints.Add((reader.GetString(0), reader.GetString(1)));
            }

            var invalidConstraints = expectedConstraints
                .Where(expected => !validatedConstraints.Contains(expected))
                .OrderBy(expected => expected.Table, StringComparer.Ordinal)
                .ThenBy(expected => expected.Name, StringComparer.Ordinal)
                .ToArray();
            if (invalidConstraints.Length > 0)
            {
                var sample = string.Join(
                    ", ",
                    invalidConstraints.Take(20).Select(item => $"{item.Table}.{item.Name}"));
                throw new InvalidOperationException(
                    "PostgreSQL migration completed without all mapped CHECK constraints being present " +
                    $"and validated. Missing or unvalidated ({invalidConstraints.Length}): {sample}.");
            }
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static char GetPostgresDeleteAction(DeleteBehavior deleteBehavior)
    {
        return deleteBehavior switch
        {
            DeleteBehavior.Cascade => 'c',
            DeleteBehavior.Restrict => 'r',
            DeleteBehavior.SetNull => 'n',
            _ => 'a'
        };
    }

    private static string NormalizeStoreType(string value)
    {
        return new string(value
                .Where(character => !char.IsWhiteSpace(character))
                .ToArray())
            .Replace("varchar", "charactervarying", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }

    private static bool IsUnexpectedLegacyColumnMismatch(
        ExpectedColumn expected,
        ExistingColumn actual)
    {
        var hasTypeMismatch =
            NormalizeStoreType(actual.StoreType) != NormalizeStoreType(expected.StoreType);
        var hasNullabilityMismatch = actual.IsNullable != expected.IsNullable;
        var hasDefaultMismatch = actual.HasDefault != expected.HasDefault;

        if (!hasTypeMismatch && !hasNullabilityMismatch && !hasDefaultMismatch)
        {
            return false;
        }

        // The first PostgreSQL baseline constrained customer-entered bank text
        // to the old 20-character bank-code shape. Allow only that exact legacy
        // width so the later free-form-bank migration can widen it safely.
        if (expected.Table == "REFUND_CLAIM"
            && expected.Column == "bankCode"
            && NormalizeStoreType(expected.StoreType) == "charactervarying(100)"
            && NormalizeStoreType(actual.StoreType) == "charactervarying(20)"
            && !hasNullabilityMismatch
            && !hasDefaultMismatch)
        {
            return false;
        }

        // The first PostgreSQL deployment predates the trigger/default migration.
        // Accept only the exact legacy rowVersion shape so the next migration can
        // backfill values and install the sequence-backed default and trigger.
        return !LegacyRowVersionColumns.Contains((expected.Table, expected.Column)) ||
               hasTypeMismatch ||
               hasNullabilityMismatch ||
               actual.HasDefault ||
               !expected.HasDefault;
    }

    private static string NormalizeSqlFragment(string? value)
    {
        var normalized = value is null
            ? string.Empty
            : new string(value
                .Where(character => !char.IsWhiteSpace(character) && character is not '(' and not ')')
                .ToArray());

        return normalized
            .Replace("::text", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("::charactervarying", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken = default)
    {
        // Chuyển giao công việc tạo dữ liệu mẫu cho lớp DbInitializer
        return DbInitializer.SeedAsync(_serviceProvider, isDevelopment);
    }
}
