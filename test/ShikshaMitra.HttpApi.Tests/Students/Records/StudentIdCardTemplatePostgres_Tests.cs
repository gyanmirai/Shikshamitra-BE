using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	private const string IcardTemplates = "/api/students/records/ids/templates";
	private const string IcardIndexPredecessor = "20261010034554_MakePublicInquiryAssignmentOptional";
	private const string IcardIndexMigration = "20261012060526_AllowMultipleStudentIdCardTemplates";

	[StudentRecordsApplicationPostgresFact]
	public async Task Icard_template_index_migration_Postgres_preserves_rows_and_later_columns_and_replays_once()
	{
		// The restored migration predates later installed migrations. Test its exact
		// predecessor-to-ID script, rather than a latest-applied-to-latest range.
		var schema = "icard_migration_" + Guid.NewGuid().ToString("N");
		await using var administrator = new NpgsqlConnection(factory.ConnectionString);
		await administrator.OpenAsync();
		await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", administrator)) await create.ExecuteNonQueryAsync();
		try
		{
			var connection = new NpgsqlConnectionStringBuilder(factory.ConnectionString) { SearchPath = schema };
			await using var database = new NpgsqlConnection(connection.ConnectionString);
			await database.OpenAsync();
			await using (var setup = new NpgsqlCommand("""
				CREATE TABLE "AppStudentIdCardTemplates" (
				  "Id" uuid PRIMARY KEY, "TenantId" uuid, "ConfigurationJson" jsonb NOT NULL,
				  "ConcurrencyStamp" text NOT NULL, "LaterSchemaValue" text NOT NULL);
				CREATE UNIQUE INDEX "IX_AppStudentIdCardTemplates_TenantId" ON "AppStudentIdCardTemplates" ("TenantId");
				CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
				""", database)) await setup.ExecuteNonQueryAsync();
			var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
			var tenant = Guid.NewGuid();
			for (var index = 0; index < ids.Length; index++)
			{
				await using var insert = new NpgsqlCommand("""
					INSERT INTO "AppStudentIdCardTemplates" VALUES (@id,@tenant,@json::jsonb,@stamp,@later)
					""", database);
				insert.Parameters.AddWithValue("id", ids[index]);
				insert.Parameters.AddWithValue("tenant", index == 0 ? tenant : Guid.NewGuid());
				insert.Parameters.AddWithValue("json", JsonSerializer.Serialize(new { name = "Legacy " + index, logo = new { reference = new string('x', 4096) }, selection = new { studentIds = ids } }));
				insert.Parameters.AddWithValue("stamp", "original-stamp-" + index);
				insert.Parameters.AddWithValue("later", "Later migration data " + index);
				await insert.ExecuteNonQueryAsync();
			}
			await using (var history = new NpgsqlCommand("INSERT INTO \"__EFMigrationsHistory\" VALUES (@id,'9.0.0')", database))
			{
				history.Parameters.AddWithValue("id", IcardIndexPredecessor); await history.ExecuteNonQueryAsync();
			}
			var before = await IcardMigrationRowsAsync(database, ids);
			await using var context = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
			context.Database.GetMigrations().ShouldContain(IcardIndexMigration);
			var script = context.GetService<IMigrator>().GenerateScript(IcardIndexPredecessor, IcardIndexMigration, MigrationsSqlGenerationOptions.Idempotent);
			await using (var migrate = new NpgsqlCommand(script, database)) await migrate.ExecuteNonQueryAsync();
			(await IcardMigrationRowsAsync(database, ids)).ShouldBe(before);
			await using (var another = new NpgsqlCommand("""
				INSERT INTO "AppStudentIdCardTemplates" VALUES (@id,@tenant,'{}','new-stamp','retained new design')
				""", database))
			{
				another.Parameters.AddWithValue("id", Guid.NewGuid()); another.Parameters.AddWithValue("tenant", tenant); await another.ExecuteNonQueryAsync();
			}
			// A second execution must be a history-guarded no-op, including with two designs for one tenant.
			await using (var replay = new NpgsqlCommand(script, database)) await replay.ExecuteNonQueryAsync();
			(await IcardMigrationRowsAsync(database, ids)).ShouldBe(before);
			await using (var index = new NpgsqlCommand("SELECT indisunique FROM pg_index WHERE indexrelid = @name::regclass", database))
			{
				index.Parameters.AddWithValue("name", $"\"{schema}\".\"IX_AppStudentIdCardTemplates_TenantId\"");
				((bool)(await index.ExecuteScalarAsync())!).ShouldBeFalse();
			}
			await using (var history = new NpgsqlCommand("SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"=@id", database))
			{
				history.Parameters.AddWithValue("id", IcardIndexMigration); ((long)(await history.ExecuteScalarAsync())!).ShouldBe(1);
			}
			await using (var count = new NpgsqlCommand("SELECT count(*) FROM \"AppStudentIdCardTemplates\"", database)) ((long)(await count.ExecuteScalarAsync())!).ShouldBe(3);
			output.WriteLine("Exact restored index migration executed twice on isolated PostgreSQL schema: legacy JSON/stamps/later columns retained, second same-tenant design accepted, one history entry.");
		}
		finally
		{
			await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", administrator); await cleanup.ExecuteNonQueryAsync();
		}
	}

	[StudentRecordsApplicationPostgresFact]
	public async Task Icard_template_Postgres_concurrent_assignment_transfers_commit_once_and_roll_back_loser_without_rewriting_other_year_or_issued_cards()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var data = await SeedIcardTemplateProviderStudentAsync();
		var source = await CreateIcardProviderTemplateAsync("Source " + Guid.NewGuid().ToString("N"), data.Year, data.Student);
		var otherYear = await CreateIcardProviderTemplateAsync("Historical " + Guid.NewGuid().ToString("N"), data.OtherYear, data.Student);
		var targets = new[] { await CreateIcardProviderTemplateAsync("Target A " + Guid.NewGuid().ToString("N")), await CreateIcardProviderTemplateAsync("Target B " + Guid.NewGuid().ToString("N")) };
		var targetIds = targets.Select(item => item.GetProperty("id").GetGuid()).ToArray();
		var sourceId = source.GetProperty("id").GetGuid(); var otherId = otherYear.GetProperty("id").GetGuid();
		var before = await InTenantAsync(async services => (await services.GetRequiredService<IRepository<StudentIdCardTemplate, Guid>>()
			.GetListAsync(item => item.Id == sourceId || item.Id == otherId || Enumerable.Contains(targetIds, item.Id)))
			.ToDictionary(item => item.Id, item => (item.ConfigurationJson, item.ConcurrencyStamp)));
		var frozenCard = await ReadIcardProviderFrozenCardAsync(data.Card);
		factory.Barrier.Arm("FROM \"AppStudentIdCardTemplates\"", afterRead: true);
		var saves = targets.Select((target, index) => Client.PutAsJsonAsync(IcardTemplates + "/" + targetIds[index], new
		{
			name = "Winner candidate " + index, schoolName = "Synthetic provider school", fields = new[] { "StudentName" },
			concurrencyStamp = target.GetProperty("concurrencyStamp").GetString(), selection = IcardProviderSelection(data.Year, data.Student)
		}, JsonOptions)).ToArray();
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		var responses = await Task.WhenAll(saves).WaitAsync(TimeSpan.FromSeconds(60));
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2);
			factory.Barrier.ConnectionIsolations.Values.ShouldAllBe(level => level == System.Data.IsolationLevel.Serializable);
			factory.Barrier.ProviderFailures.Keys.Any(state => state is "40001" or "40P01").ShouldBeTrue("The provider must report a real concurrency conflict; none is injected.");
			responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
			var winner = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.OK); var loser = 1 - winner;
			responses[loser].StatusCode.ShouldBe(HttpStatusCode.Conflict, await responses[loser].Content.ReadAsStringAsync());
			await InTenantAsync(async services =>
			{
				var rows = await services.GetRequiredService<IRepository<StudentIdCardTemplate, Guid>>()
					.GetListAsync(item => item.TenantId == TenantId);
				var loserRow = rows.Single(item => item.Id == targetIds[loser]);
				(loserRow.ConfigurationJson, loserRow.ConcurrencyStamp).ShouldBe(before[loserRow.Id]);
				var historical = rows.Single(item => item.Id == otherId);
				(historical.ConfigurationJson, historical.ConcurrencyStamp).ShouldBe(before[otherId]);
				IcardProviderAssignedStudents(rows.Single(item => item.Id == sourceId).ConfigurationJson, data.Year).ShouldBeEmpty();
				IcardProviderAssignedStudents(rows.Single(item => item.Id == targetIds[winner]).ConfigurationJson, data.Year).ShouldBe([data.Student]);
				rows.Count(item => IcardProviderAssignedStudents(item.ConfigurationJson, data.Year).Contains(data.Student)).ShouldBe(1);
				IcardProviderAssignedStudents(historical.ConfigurationJson, data.OtherYear).ShouldBe([data.Student]);
				return true;
			});
			(await ReadIcardProviderFrozenCardAsync(data.Card)).ShouldBe(frozenCard);
			output.WriteLine("Two real HTTP template transfers used separate SERIALIZABLE PostgreSQL connections: one assignment winner,409 loser with unchanged target, cross-year assignment and frozen issued card unchanged.");
		}
		finally { foreach (var response in responses) response.Dispose(); }
	}

	private static async Task<string> IcardMigrationRowsAsync(NpgsqlConnection connection, Guid[] ids)
	{
		await using var read = new NpgsqlCommand("SELECT jsonb_agg(to_jsonb(t) ORDER BY \"Id\")::text FROM \"AppStudentIdCardTemplates\" t WHERE \"Id\"=ANY(@ids)", connection);
		read.Parameters.AddWithValue("ids", ids); return (string)(await read.ExecuteScalarAsync())!;
	}
	private static object IcardProviderSelection(Guid year, Guid student) => new
	{
		academicYearId = year, boardIds = Array.Empty<Guid>(), mediumIds = Array.Empty<Guid>(),
		standardIds = Array.Empty<Guid>(), divisionIds = Array.Empty<Guid>(), studentIds = new[] { student }
	};
	private Task<JsonElement> CreateIcardProviderTemplateAsync(string name, Guid? year = null, Guid? student = null) => PostResultAsync<JsonElement>(Client, IcardTemplates, new
	{
		name, schoolName = "Synthetic provider school", fields = new[] { "StudentName" },
		selection = year.HasValue ? IcardProviderSelection(year.Value, student!.Value) : null
	});
	private static Guid[] IcardProviderAssignedStudents(string json, Guid year)
	{
		using var document = JsonDocument.Parse(json);
		if (!document.RootElement.TryGetProperty("selection", out var selection) || selection.ValueKind != JsonValueKind.Object ||
			!selection.TryGetProperty("academicYearId", out var selectedYear) || selectedYear.ValueKind != JsonValueKind.String ||
			!selectedYear.TryGetGuid(out var selectionYear) || selectionYear != year) return [];
		return selection.GetProperty("studentIds").EnumerateArray().Select(item => item.GetGuid()).ToArray();
	}
	private Task<string> ReadIcardProviderFrozenCardAsync(Guid id) => InTenantAsync(async services =>
	{
		var card = await services.GetRequiredService<IRepository<StudentIdCard, Guid>>().GetAsync(id);
		return JsonSerializer.Serialize(new { card.Id, card.StudentId, card.AcademicYearId, card.CardNumber, card.IdempotencyKey,
			card.StudentSnapshotJson, card.TemplateSnapshotJson, card.Status, card.IssuedAt, card.IssuedByUserId, card.ConcurrencyStamp });
	});
	private Task<(Guid Student, Guid Year, Guid OtherYear, Guid Card)> SeedIcardTemplateProviderStudentAsync() => InTenantAsync(async services =>
	{
		var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var year = await years.GetAsync(factory.SeededAcademicYearId);
		var historicalYear = await services.GetRequiredService<AcademicYearManager>().CreateAsync("I-card provider " + Guid.NewGuid().ToString("N"), year.StartDate.AddYears(-1), year.EndDate.AddYears(-1));
		await years.InsertAsync(historicalYear, true);
		var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(historicalYear.Id, EducationBoardType.CBSE);
		await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "I-card provider English");
		await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "I-card provider Primary");
		await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "I-card provider class", 8, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "I-card provider division", 40);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var student = new Student(Guid.NewGuid(), TenantId, "Synthetic", null, "I-card Provider", year.Id, factory.SeededBoardId,
			factory.SeededMediumId, factory.SeededMediumStageId, factory.SeededStandardId, factory.SeededDivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
		student.AssignGrNumber("PGIC-" + Guid.NewGuid().ToString("N"));
		await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
		var enrollments = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
		await enrollments.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TenantId, student.Id, year.Id, student.StandardId, student.DivisionId, 1), true);
		var historical = StudentEnrollment.Create(Guid.NewGuid(), TenantId, student.Id, historicalYear.Id, standard.Id, division.Id, 1);
		historical.Deactivate(); await enrollments.InsertAsync(historical, true);
		var reviewer = Guid.NewGuid();
		await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(reviewer, "icard-provider-" + reviewer.ToString("N"), "icard-provider-" + reviewer.ToString("N") + "@example.test", TenantId), true);
		var legacy = new StudentRecordLegacyOverride(Guid.NewGuid(), TenantId, student.Id, Guid.Parse(TestAuthHandler.TestUserId), "Verified synthetic historical enrollment for PostgreSQL assignment race", null);
		legacy.Decide(reviewer, true, "Independent synthetic review", DateTime.UtcNow);
		await services.GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().InsertAsync(legacy, true);
		var card = new StudentIdCard(Guid.NewGuid(), TenantId, student.Id, "PGIC-" + Guid.NewGuid().ToString("N"), "pg-icard-" + Guid.NewGuid().ToString("N"),
			"{\"StudentName\":\"Frozen synthetic student\"}", "{\"name\":\"Frozen issued design\"}", DateTime.UtcNow, Guid.Parse(TestAuthHandler.TestUserId), year.Id);
		await services.GetRequiredService<IRepository<StudentIdCard, Guid>>().InsertAsync(card, true);
		return (student.Id, year.Id, historicalYear.Id, card.Id);
	});
}
