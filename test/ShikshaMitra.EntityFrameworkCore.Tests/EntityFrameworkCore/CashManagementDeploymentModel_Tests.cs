using System;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

/// <summary>
/// Run with an exact class filter in its own test process, as with the canonical model check.
/// API host fixtures install additional process-wide ABP mappings after the design-time model is built.
/// This class deliberately has no ABP application fixture and lives outside the API test assembly.
/// </summary>
public class CashManagementDeploymentModel_Tests
{
	private static readonly string[] CashTables =
	[
		"Finance_CashCollections", "Finance_CashCommands", "Finance_CashDepositCredits",
		"Finance_CashDeposits", "Finance_CashExpenses"
	];

	[Fact]
	[Trait("Category", "OfflineMigrationModel")]
	public void Current_snapshot_and_latest_target_model_should_allow_EF9_migration_without_pending_changes()
	{
		// Match ShikshaMitraDbContextFactory without starting a host or opening a database connection.
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=cash_model_offline;Username=validation;Password=unused;Timeout=1")
			.Options);
		var current = db.GetService<IDesignTimeModel>().Model;
		var initializer = db.GetService<IModelRuntimeInitializer>();
		var differ = db.GetService<IMigrationsModelDiffer>();
		var sqlGenerator = db.GetService<IMigrationsSqlGenerator>();
		var migrations = db.GetService<IMigrationsAssembly>();
		migrations.Migrations.Keys.Count(name => name.EndsWith("_AddSimpleCashManagement", StringComparison.Ordinal)).ShouldBe(1);
		var snapshot = migrations.ModelSnapshot!.Model;
		var cashMigration = new AddSimpleCashManagement();
		cashMigration.TargetModel.GetEntityTypes().Select(entity => entity.GetTableName())
			.Where(table => table != null && CashTables.Contains(table)).OrderBy(table => table).ToArray()
			.ShouldBe(CashTables.OrderBy(table => table).ToArray());
		// Historical target models describe their point in the migration chain. Later
		// additive migrations must be reflected by the latest target, not backfilled here.
		var latestId = migrations.Migrations.Keys.OrderBy(name => name, StringComparer.Ordinal).Last();
		var latest = migrations.CreateMigration(migrations.Migrations[latestId], db.Database.ProviderName!);
		foreach (var saved in new[] { snapshot, latest.TargetModel })
		{
			var initialized = initializer.Initialize(saved, designTime: true);
			var differences = differ.GetDifferences(initialized.GetRelationalModel(), current.GetRelationalModel());
			var pendingSql = sqlGenerator.Generate(differences, current);
			differences.ShouldBeEmpty(string.Join(Environment.NewLine, pendingSql.Select(command => command.CommandText)));
		}
		// EF9 Migrate/MigrateAsync refuses to apply even additive migrations when this is true.
		db.Database.HasPendingModelChanges().ShouldBeFalse();
		var savedTransactions = snapshot.GetEntityTypes().Single(entity => entity.GetTableName() == "Finance_Transactions");
		savedTransactions.FindProperty("CashCollectionId")!.IsNullable.ShouldBeTrue();
		savedTransactions.GetForeignKeys().Single(key => key.Properties.Any(property => property.Name == "CashCollectionId"))
			.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
		var sql = string.Join("\n", sqlGenerator.Generate(cashMigration.UpOperations, current)
			.Select(command => command.CommandText));
		foreach (var table in CashTables) sql.ShouldContain($"CREATE TABLE \"{table}\"");
		sql.ShouldContain("ADD \"CashCollectionId\" uuid");
		Regex.IsMatch(sql, @"(?m)^\s*(DROP|TRUNCATE|UPDATE|DELETE|INSERT)\b").ShouldBeFalse();
		foreach (var destructiveOrBackfill in new[] { "ALTER COLUMN", "SET NOT NULL" })
			sql.ShouldNotContain(destructiveOrBackfill);
		db.Database.GetDbConnection().State.ShouldBe(ConnectionState.Closed);
	}
}
