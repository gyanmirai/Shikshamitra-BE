using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Content;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Migrations;
using ShikshaMitra.Infrastructure.Auth;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;


namespace ShikshaMitra.Academic.Mobile;

public sealed class MobileAcademicPostgresFactAttribute : FactAttribute
{
	public MobileAcademicPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(MobileAcademicPostgresFactory.ConnectionVariable)))
			Skip = "Requires a fresh disposable loopback mobile_academic_tests_* database in MOBILE_ACADEMIC_TEST_POSTGRES.";
	}
}

public sealed class MobileAcademicPostgresFactory : ShikshaMitraApiTestFactory
{
	public const string ConnectionVariable = "MOBILE_ACADEMIC_TEST_POSTGRES";
	public MobileLeaveLockBarrier Barrier { get; } = new();
	public string ConnectionString { get; private set; } = "";
	protected override bool InitializeTestData => false;
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:Default"] = Environment.GetEnvironmentVariable(ConnectionVariable)
		}));
	}
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^mobile_academic_tests_[a-z0-9_]{1,40}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Only a fresh disposable loopback mobile_academic_tests_* database is allowed.");
		ConnectionString = connection.ConnectionString;
		using (var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString))
		{
			admin.Open();
			using var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", admin);
			exists.Parameters.AddWithValue("name", connection.Database);
			if ((long)exists.ExecuteScalar()! != 0) throw new InvalidOperationException("Refusing to reuse or reset an existing database.");
			using var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin);
			create.ExecuteNonQuery();
		}
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(ConnectionString).AddInterceptors(Barrier)));
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(ConnectionString).Options);
		db.GetService<IRelationalDatabaseCreator>().CreateTables();
		// Recreate the predecessor column state in this new test database, then apply the actual migration.
		var migration = new MobileAcademicResponses { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
		foreach (var operation in migration.UpOperations.Cast<Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation>())
			db.Database.ExecuteSqlRaw($"ALTER TABLE \"{operation.Table}\" DROP COLUMN \"{operation.Name}\"");
		foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model))
			db.Database.ExecuteSqlRaw(command.CommandText);
		var history = db.GetService<IHistoryRepository>();
		db.Database.ExecuteSqlRaw(history.GetCreateIfNotExistsScript());
		db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow("20261012061916_MobileAcademicResponses", "9.0.0")));
		var repair = new RepairMobileLeaveHalfDayTimes { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
		var repairCommands = db.GetService<IMigrationsSqlGenerator>().Generate(repair.UpOperations, db.Model);
		// Cover both a complete initial migration and the earlier applied three-column version.
		foreach (var command in repairCommands) db.Database.ExecuteSqlRaw(command.CommandText);
		db.Database.ExecuteSqlRaw("ALTER TABLE \"AppStudentLeaves\" DROP COLUMN \"HalfDayStartTime\", DROP COLUMN \"HalfDayEndTime\"");
		foreach (var command in repairCommands) db.Database.ExecuteSqlRaw(command.CommandText);
		foreach (var command in repairCommands) db.Database.ExecuteSqlRaw(command.CommandText);
		db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow("20261012113000_RepairMobileLeaveHalfDayTimes", "9.0.0")));
		db.Database.GetMigrations().ShouldContain("20261012113000_RepairMobileLeaveHalfDayTimes");
		db.Database.GetAppliedMigrations().ShouldContain("20261012113000_RepairMobileLeaveHalfDayTimes");
		var submissionMigration = new MobileHomeworkSubmissionReliability { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
		var acknowledgmentMigration = new MobileHomeworkCompletionAcknowledgment { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
		foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(acknowledgmentMigration.DownOperations, db.Model))
			db.Database.ExecuteSqlRaw(command.CommandText);
		foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(submissionMigration.DownOperations, db.Model))
			db.Database.ExecuteSqlRaw(command.CommandText);
		foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(submissionMigration.UpOperations, db.Model))
			db.Database.ExecuteSqlRaw(command.CommandText);
		db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow("20261021110849_MobileHomeworkSubmissionReliability", "9.0.0")));
		foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(acknowledgmentMigration.UpOperations, db.Model))
			db.Database.ExecuteSqlRaw(command.CommandText);
		db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow("20261022072327_MobileHomeworkCompletionAcknowledgment", "9.0.0")));
	}
}

public sealed class MobileLeaveLockBarrier : DbCommandInterceptor
{
	private string? reviewCandidate;
	public TaskCompletionSource ReviewReached { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public TaskCompletionSource ReviewCommitted { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public void ArmReview(string candidate)
	{
		reviewCandidate = candidate;
		ReviewReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
		ReviewCommitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
	}
	public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
		InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
	{
		if (reviewCandidate != null && command.CommandText.Contains("UPDATE \"academic_StudentHomeworkAssignmentSubmissions\"", StringComparison.Ordinal) &&
			command.Parameters.Cast<DbParameter>().Any(p => p.Value is string value && value == reviewCandidate))
		{
			reviewCandidate = null;
			ReviewReached.TrySetResult();
			await ReviewCommitted.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
		}
		return result;
	}
	private int armed;
	private int arrived;
	private TaskCompletionSource signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public ConcurrentDictionary<int, byte> Connections { get; } = new();
	public void Arm() { arrived = 0; Connections.Clear(); signal = new(TaskCreationOptions.RunContinuationsAsynchronously); Volatile.Write(ref armed, 1); }
	public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
		InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		if (Volatile.Read(ref armed) == 1 && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
		{
			Connections.TryAdd(((NpgsqlConnection)command.Connection!).ProcessID, 0);
			if (Interlocked.Increment(ref arrived) == 2) { Volatile.Write(ref armed, 0); signal.TrySetResult(); }
			await signal.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
		}
		return result;
	}
}

public sealed partial class MobileAcademicPostgresApi_Tests
{
	private IServiceProvider Services = null!;
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private async Task<T> Insert<T>(T entity) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
		await Services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, true);
	private record Fixture(Student Student, IdentityUser User, StudentPortalAccountLink Link, StudentEnrollment Enrollment, AcademicYear Year, AssignmentEntity Assignment, Exam Exam, Guid SubjectId, Guid ChapterId);
	private async Task<Fixture> Seed(Guid tenantId, bool parent)
	{
		var today = MobileAcademicContextAppService.SchoolToday;
		var year = await Insert(await Services.GetRequiredService<AcademicYearManager>().CreateAsync("Current", today.AddDays(-30), today.AddDays(300)));
		var board = await Insert(await Services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE));
		var medium = await Insert(await Services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"));
		var stage = await Insert(await Services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"));
		var standard = await Insert(await Services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 8", 12, null));
		var division = await Insert(await Services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 30));
		var student = await Insert(new Student(Guid.NewGuid(), tenantId, "Fixture", "", "Student", year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id, rollNo: 12));
		var enrollment = await Insert(StudentEnrollment.Create(Guid.NewGuid(), tenantId, student.Id, year.Id, standard.Id, division.Id, 12));
		var user = new IdentityUser(Guid.NewGuid(), "mobile-fixture", "mobile@example.test", tenantId);
		user.SetEmailConfirmed(true);
		await Insert(user);
		var link = await Insert(new StudentPortalAccountLink(Guid.NewGuid(), tenantId, student.Id, user.Id, parent ? "guardian" : "student", parent ? "mother" : "self", Guid.NewGuid()));
		var subject = await Insert(new Subject(Guid.NewGuid(), tenantId, "Mathematics", "#008967", medium.Id));
		var assigned = await Insert(new AssignedSubject(Guid.NewGuid(), tenantId, standard.Id, subject.Id, "MATH", SubjectStatus.Published));
		var chapter = await Insert(new AssignedSubjectChapter(Guid.NewGuid(), assigned.Id, 1, "Fractions", today, today.AddDays(10))
			{ AssignedSubjectId = assigned.Id, TenantId = tenantId, PublishStatus = "Published", Visibility = "All", ParentSummary = "School family guidance" });
		var type = await Services.GetRequiredService<AssignmentTypeManager>().CreateAsync(medium.Id, "Practice", defaultMarks: 20, defaultEvaluationMethod: EvaluationMethod.Marks);
		var assignment = new AssignmentEntity(Guid.NewGuid(), standard.Id, subject.Id, "Practice fractions", "Solve the supplied questions.", type.Id, SubmissionType.OnlineText, 20, tenantId, year.Id);
		assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), assignment.Id, division.Id, today, DateTime.UtcNow.AddDays(3), tenantId));
		assignment.PublishAll();
		await Insert(assignment);
		var examType = await Insert(new ExamType(Guid.NewGuid(), tenantId, "Unit test", null, medium.Id));
		var exam = new Exam(Guid.NewGuid(), examType.Id, "Fractions test", standard.Id, tenantId);
		exam.AddDivision(Guid.NewGuid(), division.Id);
		exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subject.Id, today.AddDays(-1), today.AddHours(9), today.AddHours(10), 20, 7));
		exam.ChangeStatus(ExamStatus.Completed);
		await Insert(exam);
		return new(student, user, link, enrollment, year, assignment, exam, subject.Id, chapter.Id);
	}

	[MobileAcademicPostgresFact]
	public async Task Mobile_HTTP_routes_migration_files_and_concurrent_leave_requests_use_real_Postgres()
	{
		await using var factory = new MobileAcademicPostgresFactory();
		using var client = factory.CreateClient();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		Fixture f; Guid leaveType; Guid resultId;
		using (var scope = factory.Services.CreateScope())
		{
			Services = scope.ServiceProvider;
			using var uow = Services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
			var school = await Services.GetRequiredService<Volo.Abp.TenantManagement.TenantManager>().CreateAsync("Synthetic mobile school");
			tenantId = school.Id;
			await Services.GetRequiredService<Volo.Abp.TenantManagement.ITenantRepository>().InsertAsync(school, true);
			using var tenant = Services.GetRequiredService<ICurrentTenant>().Change(tenantId);
			f = await Seed(tenantId, true);
			var date = MobileAcademicContextAppService.SchoolToday.AddDays(1);
			var calendar = new AcademicCalendar(Guid.NewGuid(), tenantId, f.Student.AcademicMediumId, f.Year.Id, "Synthetic calendar", f.Year.StartDate, f.Year.EndDate, isDefault: true);
			calendar.Publish(f.User.Id); await Insert(calendar);
			await Insert(new AcademicCalendarDay(Guid.NewGuid(), tenantId, calendar.Id, date, DayType.WorkingDay, "Teaching day"));
			leaveType = (await Insert(await Services.GetRequiredService<StudentLeaveTypeManager>().CreateAsync("Medical", "#008967"))).Id;
			var header = await Insert(new ExamResultHeader(Guid.NewGuid(), f.Exam.Id, f.Enrollment.DivisionId!.Value, f.User.Id, tenantId));
			await Insert(new ExamResultDetail(Guid.NewGuid(), header.Id, f.Exam.Id, f.Student.Id, f.Enrollment.DivisionId.Value, 20, 18, 90, "A", 1, 1, ExamStudentResultStatus.Pass, tenantId));
			header.Publish(f.User.Id);
			await Services.GetRequiredService<IRepository<ExamResultHeader, Guid>>().UpdateAsync(header, true);
			resultId = header.Id;
			await uow.CompleteAsync();
		}
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, f.User.Id.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "parent");
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		client.DefaultRequestHeaders.Add("__tenant", tenantId.ToString());
		var query = $"?studentId={f.Student.Id}&academicYearId={f.Year.Id}";
		var month = DateTime.Today;
		foreach (var path in new[]
		{
			"/api/mobile/v1/academics/context", "/api/mobile/v1/academics/dashboard",
			"/api/mobile/v1/academics/homework", "/api/mobile/v1/academics/attendance/overview",
			"/api/mobile/v1/academics/attendance/calendar",
			"/api/mobile/v1/academics/syllabus/dashboard", "/api/mobile/v1/exams", "/api/mobile/v1/results",
			"/api/mobile/v1/academics/holidays-events"
		})
		{
			using var response = await client.GetAsync(path + query + $"&year={month.Year}&month={month.Month}");
			response.StatusCode.ShouldBe(HttpStatusCode.OK, path + ": " + await response.Content.ReadAsStringAsync());
			(await response.Content.ReadAsStringAsync()).ShouldContain("\"success\":true");
			if (path == "/api/mobile/v1/academics/syllabus/dashboard")
			{
				using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
				var subject = json.RootElement.GetProperty("data").GetProperty("subjects").EnumerateArray().Single();
				subject.GetProperty("id").GetGuid().ShouldBe(f.SubjectId);
				subject.GetProperty("color").GetString().ShouldBe("#008967");
			}
		}
		using (var stranger = factory.CreateClient())
		{
			stranger.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
			stranger.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
			stranger.DefaultRequestHeaders.Add("__tenant", tenantId.ToString());
			using var denied = await stranger.GetAsync("/api/mobile/v1/academics/dashboard" + query);
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		var input = new MobileLeaveInput
		{
			StudentId = f.Student.Id, AcademicYearId = f.Year.Id, LeaveTypeId = leaveType,
			StartDate = DateOnly.FromDateTime(MobileAcademicContextAppService.SchoolToday.AddDays(1)),
			EndDate = DateOnly.FromDateTime(MobileAcademicContextAppService.SchoolToday.AddDays(1)),
			IsHalfDay = true, HalfDayStartTime = new(8, 30), HalfDayEndTime = new(12, 30),
			Description = "Synthetic appointment"
		};
		factory.Barrier.Arm();
		var responses = await Task.WhenAll(
			client.PostAsJsonAsync("/api/mobile/v1/academics/attendance/leave-requests", input, JsonOptions),
			client.PostAsJsonAsync("/api/mobile/v1/academics/attendance/leave-requests", input, JsonOptions)).WaitAsync(TimeSpan.FromSeconds(60));
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2, "Both requests must reach the real PostgreSQL transaction lock on different connections.");
			responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1, string.Join("\n", await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()))));
			var rejected = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
			rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
			var saved = (await responses.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<Result<MobileLeaveDto>>(JsonOptions))!.Data!;
			saved.DurationDays.ShouldBe(.5m); saved.HalfDayStartTime.ShouldBe(new TimeOnly(8, 30));
			using var leaves = await client.GetAsync("/api/mobile/v1/academics/attendance/leave-requests" + query);
			var page = (await leaves.Content.ReadFromJsonAsync<Result<MobilePage<MobileLeaveDto>>>(JsonOptions))!.Data!;
			page.TotalCount.ShouldBe(1); page.Items.Single().Id.ShouldBe(saved.Id);
			using var cancel = await client.PatchAsJsonAsync($"/api/mobile/v1/academics/attendance/leave-requests/{saved.Id}/status", input, JsonOptions);
			cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
		}
		finally { foreach (var response in responses) response.Dispose(); }
		using var uploadBody = new System.Net.Http.MultipartFormDataContent();
		var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\nSynthetic mobile attachment");
		var content = new System.Net.Http.ByteArrayContent(bytes); content.Headers.ContentType = new("application/pdf");
		uploadBody.Add(content, "file", "synthetic-answer.pdf");
		using var upload = await client.PostAsync($"/api/mobile/v1/academics/homework/{f.Assignment.Id}/submission/attachments" + query, uploadBody);
		upload.StatusCode.ShouldBe(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
		var attachment = (await upload.Content.ReadFromJsonAsync<Result<MobileAttachmentDto>>(JsonOptions))!.Data!;
		using var download = await client.GetAsync($"/api/mobile/v1/academics/homework/{f.Assignment.Id}/attachments/{attachment.Id}/download" + query);
		download.StatusCode.ShouldBe(HttpStatusCode.OK, await download.Content.ReadAsStringAsync());
		(await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
		using var delete = await client.DeleteAsync($"/api/mobile/v1/academics/homework/{f.Assignment.Id}/submission/attachments/{attachment.Id}" + query);
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, await delete.Content.ReadAsStringAsync());
		using var report = await client.GetAsync($"/api/mobile/v1/results/{resultId}/report-card/download" + query);
		report.StatusCode.ShouldBe(HttpStatusCode.OK, await report.Content.ReadAsStringAsync());
		Encoding.ASCII.GetString((await report.Content.ReadAsByteArrayAsync()).Take(5).ToArray()).ShouldBe("%PDF-");
		await CheckHomeworkSubmissionsAsync(client, f, factory, tenantId);
		await CheckCompletionAcknowledgmentAsync(client, f, factory, tenantId);
	}
}

