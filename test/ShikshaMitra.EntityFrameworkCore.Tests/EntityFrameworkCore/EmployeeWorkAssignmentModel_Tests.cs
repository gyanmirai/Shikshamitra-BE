using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.Migrations;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class EmployeeWorkAssignmentModel_Tests
{
	[Fact]
	public void Assignment_migration_is_additive_and_current_snapshot_matches_application_table()
	{
		var migration = new AddEmployeeApplicationWorkAssignment();
		Assert.Equal(5, migration.UpOperations.Count);
		Assert.All(migration.UpOperations, operation =>
			Assert.Equal("AppEmployeeApplications", Assert.IsType<AddColumnOperation>(operation).Table));
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		// Model inspection only: no connection is opened and no migration is applied.
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Database=work_assignment_model_only").Options);
		var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(
			db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
		var current = db.GetService<IDesignTimeModel>().Model;
		var entity = current.FindEntityType(typeof(EmployeeApplication))!;
		Assert.Equal("uuid[]", entity.FindProperty(nameof(EmployeeApplication.WorkAssignmentOrganizationIds))!.GetColumnType());
		Assert.Equal("text[]", entity.FindProperty(nameof(EmployeeApplication.WorkAssignmentOrganizationLabels))!.GetColumnType());
		var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
			snapshot.GetRelationalModel(), current.GetRelationalModel());
		Assert.DoesNotContain(differences.OfType<ColumnOperation>(), operation => operation.Table == "AppEmployeeApplications");
	}
}
