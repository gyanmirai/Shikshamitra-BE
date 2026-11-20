using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Library;

public sealed class LibraryPostgresFactAttribute : FactAttribute
{
    public LibraryPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LibraryMigrationPostgresFixture.ConnectionVariable)))
            Skip = "Set LIBRARY_TEST_POSTGRES to a loopback PostgreSQL administrator connection for disposable library_tests_* databases.";
    }
}

public sealed class LibraryPostgresTheoryAttribute : TheoryAttribute
{
    public LibraryPostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LibraryMigrationPostgresFixture.ConnectionVariable)))
            Skip = "Set LIBRARY_TEST_POSTGRES to run the isolated PostgreSQL migration scenarios.";
    }
}

internal sealed class LibraryMigrationPostgresFixture : IAsyncDisposable
{
    public const string ConnectionVariable = "LIBRARY_TEST_POSTGRES";
    public const string DataDirectoryVariable = "LIBRARY_TEST_POSTGRES_DATA_DIRECTORY";
    public const string Previous = "20261104183148_ReconcileEmployeeWorkShiftModel";
    public const string Foundation = "20261116105218_CompleteLibraryFoundation";
    public const string PolicyInbox = "20261116124311_AddLibraryPoliciesAndRecipientInbox";
    public const string CopyEvidence = "20261117053040_AddLibraryCopyLifecycleEvidence";
    public const string CirculationEvidence = "20261117151223_CompleteLibraryCirculationAndFinanceSources";
    public const string TypedSettlement = "20261117182446_CompleteLibraryTypedSettlementAndRecordedStaffScope";
    public const string StockReceiving = "20261118051353_AddLibraryStockReceivingEvidence";
	public const string CatalogueConfiguration = "20261118112116_ConfigureSchoolLibraryCatalogue";
	public const string CataloguePrice = "20261118165933_AllowUnknownLibraryCataloguePrice";
	public const string PositionEvidence = "20261118195300_AddFinanceExternalPositionEvidenceFunctions";
	public const string SourceFingerprintEvidence = "20261118223401_AddFinanceExternalSourceFingerprintFunctions";
    public const string TenantA = "a0000000-0000-0000-0000-000000000001";
    public const string TenantB = "b0000000-0000-0000-0000-000000000002";
    private readonly string administratorConnection;
    private readonly string databaseName;
    private readonly string connectionString;
    private bool created;

    private LibraryMigrationPostgresFixture()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database != "postgres")
            throw new InvalidOperationException("Library migration tests require a loopback administrator connection to postgres.");
        connection.Pooling = false;
        administratorConnection = connection.ConnectionString;
        databaseName = $"library_tests_migration_{Guid.NewGuid():N}";
        connection.Database = databaseName;
        connectionString = connection.ConnectionString;
    }

    public static async Task<LibraryMigrationPostgresFixture> CreateAsync()
    {
        var fixture = new LibraryMigrationPostgresFixture();
        await using var sql = new NpgsqlConnection(fixture.administratorConnection);
        await sql.OpenAsync();
        await ValidateServerAsync(sql);
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{fixture.databaseName}\"", sql);
        await create.ExecuteNonQueryAsync();
        fixture.created = true;
        await fixture.ExecuteAsync(LegacySchema);
        return fixture;
    }

    internal static async Task<LibraryMigrationPostgresFixture> CreateFullHistoryAsync(string targetMigration)
    {
        var fixture = new LibraryMigrationPostgresFixture();
        await using var connection = new NpgsqlConnection(fixture.administratorConnection);
        await connection.OpenAsync();
        await ValidateServerAsync(connection);
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{fixture.databaseName}\"", connection);
        await create.ExecuteNonQueryAsync();
        fixture.created = true;
        try
        {
            await using var context = fixture.CreateContext();
            context.Database.SetCommandTimeout(180);
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal async Task<LibraryMigrationPostgresFixture> CloneAsync()
    {
        var clone = new LibraryMigrationPostgresFixture();
        await using var connection = new NpgsqlConnection(administratorConnection);
        await connection.OpenAsync();
        await ValidateServerAsync(connection);
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{clone.databaseName}\" TEMPLATE \"{databaseName}\"", connection);
        await create.ExecuteNonQueryAsync();
        clone.created = true;
        return clone;
    }

    internal async Task MigrateToCurrentAsync()
    {
        await using var context = CreateContext();
        context.Database.SetCommandTimeout(180);
        await context.GetService<IMigrator>().MigrateAsync();
    }

    public async Task ExecuteAsync(string statement, int timeoutSeconds = 30)
    {
        await using var sql = new NpgsqlConnection(connectionString);
        await sql.OpenAsync();
        await using var command = new NpgsqlCommand(statement, sql);
        command.CommandTimeout = timeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string?> ScalarAsync(string statement)
    {
        await using var sql = new NpgsqlConnection(connectionString);
        await sql.OpenAsync();
        await using var command = new NpgsqlCommand(statement, sql);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : value.ToString();
    }

    public ShikshaMitraDbContext CreateContext()
    {
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        return new(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connectionString).Options);
    }

    public string Script(bool rollback = false)
        => Script(rollback ? Foundation : Previous, rollback ? Previous : Foundation);

    public string Script(string fromMigration, string toMigration)
    {
        using var db = CreateContext();
        return db.GetService<IMigrator>().GenerateScript(fromMigration, toMigration);
    }

    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        await using var sql = new NpgsqlConnection(administratorConnection);
        await sql.OpenAsync();
        await ValidateServerAsync(sql);
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\"", sql);
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task ValidateServerAsync(NpgsqlConnection connection)
    {
        var expected = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (string.IsNullOrWhiteSpace(expected)) return;
        await using var query = new NpgsqlCommand("SELECT current_setting('data_directory')", connection);
        var actual = (string?)await query.ExecuteScalarAsync();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (actual == null || !string.Equals(Path.GetFullPath(expected).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), comparison))
            throw new InvalidOperationException("The PostgreSQL server is not the expected owned Library test directory. No database operation was performed.");
    }

    private const string LegacySchema = """
        CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
        INSERT INTO "__EFMigrationsHistory" VALUES ('20261104183148_ReconcileEmployeeWorkShiftModel', '9.0.0');
        CREATE TABLE "AppBooks" ("Id" uuid PRIMARY KEY, "TenantId" uuid, "NumberOfCopies" integer NOT NULL);
        CREATE TABLE "AppBookCopies" (
            "Id" uuid PRIMARY KEY, "BookId" uuid NOT NULL REFERENCES "AppBooks"("Id"),
            "AccessionNumber" varchar(50) NOT NULL, "Status" integer NOT NULL, "Remarks" text
        );
        CREATE UNIQUE INDEX "IX_AppBookCopies_AccessionNumber" ON "AppBookCopies"("AccessionNumber");
        CREATE TABLE "AppLibraryNotificationSettings" (
            "Id" uuid PRIMARY KEY, "TenantId" uuid, "EventType" text NOT NULL, "RecipientRole" text NOT NULL, "Channel" text NOT NULL
        );
        CREATE UNIQUE INDEX "IX_AppLibraryNotificationSettings_EventType_RecipientRole_Chan~"
            ON "AppLibraryNotificationSettings"("EventType", "RecipientRole", "Channel");
        CREATE TABLE "AppLibraryNotificationHistories" (
            "Id" uuid PRIMARY KEY, "TenantId" uuid, "Status" text NOT NULL, "DeduplicationKey" text,
            "Channel" text NOT NULL DEFAULT 'Email'
        );
        INSERT INTO "AppBooks" VALUES
            ('10000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 99),
            ('10000000-0000-0000-0000-000000000002', 'b0000000-0000-0000-0000-000000000002', 99),
            ('10000000-0000-0000-0000-000000000003', NULL, 99),
            ('10000000-0000-0000-0000-000000000004', 'a0000000-0000-0000-0000-000000000001', 99);
        INSERT INTO "AppBookCopies" VALUES
            ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'Acc-001', 4, 'Existing damage evidence'),
            ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', 'acc-001', 0, NULL),
            ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003', 'Host-001', 0, NULL);
        INSERT INTO "AppLibraryNotificationSettings" VALUES
            ('30000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 'dueDateReminder', 'Member', 'Email');
        INSERT INTO "AppLibraryNotificationHistories" ("Id", "TenantId", "Status", "DeduplicationKey") VALUES
            ('40000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 'Sent', 'original-history');
        """;
}
