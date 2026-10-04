using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class VariableFeeMigration_Tests
{
    [FinanceMigrationPostgresFact]
    public async Task PostgreSql_Upgrade_Preserves_Fixed_Amounts_Rejects_Contradictory_Pricing_And_Protects_Rollback()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FINANCE_MIGRATION_TEST_POSTGRES"));
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Requires a separate empty disposable loopback payroll_tests_* database.");
        await using var sql = new NpgsqlConnection(connection.ConnectionString);
        await sql.OpenAsync();
        async Task<object?> Scalar(string query) => await new NpgsqlCommand(query, sql).ExecuteScalarAsync();
        async Task Execute(string query) => await new NpgsqlCommand(query, sql).ExecuteNonQueryAsync();
        ((long)(await Scalar("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')"))!).ShouldBe(0);
        await Execute("CREATE TABLE \"Finance_FeeStructureDetails\" (\"Id\" uuid PRIMARY KEY, \"Amount\" numeric(18,2) NOT NULL)");
        await Execute("INSERT INTO \"Finance_FeeStructureDetails\" VALUES ('11111111-1111-1111-1111-111111111111', 1234.56)");
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
        var migration = new VariableFeeStructurePricing();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(migration.UpOperations)) await Execute(command.CommandText);
        (await Scalar("SELECT \"Amount\" FROM \"Finance_FeeStructureDetails\"" )).ShouldBe(1234.56m);
        (await Scalar("SELECT \"IsVariable\" FROM \"Finance_FeeStructureDetails\"" )).ShouldBe(false);
        await Execute("INSERT INTO \"Finance_FeeStructureDetails\" VALUES ('22222222-2222-2222-2222-222222222222', NULL, true)");
        foreach (var pricing in new[] { "NULL, false", "0, true", "-1, false", "10000000000, false" })
        {
            var rejected = await Should.ThrowAsync<PostgresException>(() => Execute($"INSERT INTO \"Finance_FeeStructureDetails\" VALUES (gen_random_uuid(), {pricing})"));
            rejected.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        }
        var before = await Scalar("SELECT jsonb_agg(to_jsonb(d) ORDER BY \"Id\")::text FROM \"Finance_FeeStructureDetails\" d");
        var down = generator.Generate(migration.DownOperations);
        var protectedRollback = await Should.ThrowAsync<PostgresException>(() => Execute(down.First().CommandText));
        protectedRollback.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
        protectedRollback.MessageText.ShouldContain("use a forward fix");
        (await Scalar("SELECT jsonb_agg(to_jsonb(d) ORDER BY \"Id\")::text FROM \"Finance_FeeStructureDetails\" d")).ShouldBe(before);
        await Execute("DELETE FROM \"Finance_FeeStructureDetails\" WHERE \"IsVariable\"");
        foreach (var command in down) await Execute(command.CommandText);
        (await Scalar("SELECT \"Amount\" FROM \"Finance_FeeStructureDetails\"" )).ShouldBe(1234.56m);
    }

    [Fact]
    public void Migration_Preserves_Fixed_Default_And_Protects_Used_Variable_Pricing_On_Rollback()
    {
        var migration = new VariableFeeStructurePricing();
        var flag = migration.UpOperations.OfType<AddColumnOperation>().Single();
        flag.Name.ShouldBe("IsVariable");
        flag.DefaultValue.ShouldBe(false);
        migration.UpOperations.OfType<AlterColumnOperation>().Single().IsNullable.ShouldBeTrue();
        migration.UpOperations.OfType<AddCheckConstraintOperation>().Single().Sql.ShouldContain("\"Amount\" IS NULL");
        migration.DownOperations.First().ShouldBeOfType<SqlOperation>().Sql.ShouldContain("use a forward fix");
        migration.DownOperations.OfType<AlterColumnOperation>().Single().DefaultValue.ShouldBeNull();
    }

    [Fact]
    public void Model_Requires_Null_Only_For_Variable_Structure_Amounts()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=payroll_tests_model_only").Options);
        var detail = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(FeeStructureDetail))!;
        detail.FindProperty(nameof(FeeStructureDetail.Amount))!.IsNullable.ShouldBeTrue();
        detail.FindProperty(nameof(FeeStructureDetail.IsVariable))!.GetDefaultValue().ShouldBe(false);
        detail.GetCheckConstraints().Single(c => c.Name == "CK_FeeStructureDetail_Pricing").Sql.ShouldContain("NOT \"IsVariable\"");
    }
}
