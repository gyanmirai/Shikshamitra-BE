using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.Migrations;
using ShikshaMitra.TenantSettings.EducationBoards;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class AcademicStageOrderRepairMigration_Tests
{
	private const string RepairId = "20261026110754_RepairAcademicStageOrderColumn";

	[Fact]
	public void Ef_discovers_the_additive_repair_and_preserves_the_preceding_target_model()
	{
		using var db = CreateContext();
		var migrations = db.GetService<IMigrationsAssembly>();
		Assert.True(migrations.Migrations.TryGetValue(RepairId, out var type));
		var migration = migrations.CreateMigration(type!, "Npgsql.EntityFrameworkCore.PostgreSQL");
		Assert.IsType<RepairAcademicStageOrderColumn>(migration);
		var initializer = db.GetService<IModelRuntimeInitializer>();
		var target = initializer.Initialize(migration.TargetModel, designTime: true);
		var preceding = initializer.Initialize(new AddAcademicSiblingOrder().TargetModel, designTime: true);
		Assert.Empty(db.GetService<IMigrationsModelDiffer>().GetDifferences(
			preceding.GetRelationalModel(), target.GetRelationalModel()));
		var stage = target.FindEntityType(typeof(EducationMediumStage));
		Assert.NotNull(stage);
		Assert.Equal("academic_EducationMediumStages", stage.GetTableName());
		var sortOrder = stage.FindProperty("SortOrder");
		Assert.NotNull(sortOrder);
		Assert.Equal(typeof(int), sortOrder.ClrType);
		Assert.False(sortOrder.IsNullable);
		Assert.Equal(0, sortOrder.GetDefaultValue());
		Assert.Contains(stage.GetIndexes(), index => index.Properties.Select(property => property.Name)
			.SequenceEqual(new[] { "TenantId", "EducationMediumId", "SortOrder" }));
		Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
	}

	[Fact]
	public void Latest_migration_target_matches_the_current_model_without_connecting_to_a_database()
	{
		using var db = CreateContext();
		var migrations = db.GetService<IMigrationsAssembly>();
		var latest = migrations.Migrations.OrderBy(entry => entry.Key).Last();
		var migration = migrations.CreateMigration(latest.Value, "Npgsql.EntityFrameworkCore.PostgreSQL");
		var target = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
		Assert.Empty(db.GetService<IMigrationsModelDiffer>().GetDifferences(target.GetRelationalModel(),
			db.GetService<IDesignTimeModel>().Model.GetRelationalModel()));
		Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
	}

	[Fact]
	public void Repair_does_not_replay_original_class_or_division_schema_changes()
	{
		var operation = Assert.IsType<SqlOperation>(Assert.Single(new RepairAcademicStageOrderColumn().UpOperations));
		Assert.DoesNotContain("academic_Standards", operation.Sql);
		Assert.DoesNotContain("academic_Divisions", operation.Sql);
	}

	[Fact]
	public void Rollback_preserves_the_schema_and_data_owned_by_the_preceding_target()
	{
		var repair = new RepairAcademicStageOrderColumn();
		Assert.Empty(repair.DownOperations);
		using var db = CreateContext();
		var initializer = db.GetService<IModelRuntimeInitializer>();
		var before = initializer.Initialize(new AddAcademicSiblingOrder().TargetModel, designTime: true);
		var after = initializer.Initialize(repair.TargetModel, designTime: true);
		Assert.Empty(db.GetService<IMigrationsModelDiffer>().GetDifferences(before.GetRelationalModel(), after.GetRelationalModel()));
	}

	private static ShikshaMitraDbContext CreateContext()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		return new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Database=stage_repair_model_check;Username=unused;Password=unused",
				options => options.MigrationsAssembly(typeof(RepairAcademicStageOrderColumn).Assembly.GetName().Name)).Options);
	}
}
