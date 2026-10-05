using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Migrations;
using Shouldly;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class RubricCriterionSchemaPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Legacy_Criteria_Schema_Should_Fail_And_Migration_Should_Restore_Points_And_Identity()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ACADEMIC_PERF_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("academic_tests_rubrics_", StringComparison.Ordinal) != true)
		{
			throw new InvalidOperationException("Rubric tests require a new, empty loopback academic_tests_rubrics_* database.");
		}
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var database = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connection.ConnectionString).Options);
		// Refuse existing tables. Only this disposable test database is changed.
		database.GetService<IRelationalDatabaseCreator>().CreateTables();
		var rubricId = Guid.NewGuid();
		var criterionId = Guid.NewGuid();
		var tenantId = Guid.NewGuid();
		await database.Database.ExecuteSqlRawAsync("""
			INSERT INTO "academic_AssessmentRubrics"
			("Id", "TenantId", "IsTemplate", "ScoringModel", "Visibility", "Title", "TotalMarks", "Version", "Status", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
			VALUES ({0}, {1}, true, 0, 3, 'Schema regression', 10, 1, 0, '{{}}', 'schema-regression', NOW(), false);
			INSERT INTO "academic_AssessmentRubricCriteria"
			("Id", "TenantId", "RubricId", "Title", "Points", "DisplayOrder", "IsRequired", "CreationTime", "IsDeleted")
			VALUES ({2}, {1}, {0}, 'Accuracy', 10, 1, true, NOW(), false);
			""", rubricId, tenantId, criterionId);

		async Task<List<AssessmentRubricCriterion>> ReadCriteriaAsync() => await database.Set<AssessmentRubricCriterion>()
			.IgnoreQueryFilters().AsNoTracking().Where(criterion => criterion.TenantId == tenantId && criterion.RubricId == rubricId).ToListAsync();
		(await ReadCriteriaAsync()).ShouldContain(c => c.Id == criterionId && c.Points == 10);
		var migration = new RemoveRubricCriterionMaxMarks();
		var generator = database.GetService<IMigrationsSqlGenerator>();
		await database.Database.OpenConnectionAsync();
		foreach (var sql in generator.Generate(migration.DownOperations, database.Model))
		{
			using var command = database.Database.GetDbConnection().CreateCommand();
			command.CommandText = sql.CommandText;
			await command.ExecuteNonQueryAsync();
		}
		var error = await Should.ThrowAsync<PostgresException>(ReadCriteriaAsync);
		error.SqlState.ShouldBe("42703");
		error.MessageText.ShouldContain("Points");
		foreach (var sql in generator.Generate(migration.UpOperations, database.Model))
		{
			using var command = database.Database.GetDbConnection().CreateCommand();
			command.CommandText = sql.CommandText;
			await command.ExecuteNonQueryAsync();
		}
		(await ReadCriteriaAsync()).ShouldContain(c => c.Id == criterionId && c.Points == 10 && c.RubricId == rubricId);
	}
}
