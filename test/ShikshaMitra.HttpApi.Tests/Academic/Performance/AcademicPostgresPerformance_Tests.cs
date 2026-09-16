using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Academic.Performance;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AcademicPerformanceCollection
{
	public const string Name = "Academic PostgreSQL performance";
}

[Collection(AcademicPerformanceCollection.Name)]
public sealed class AcademicPostgresPerformance_Tests(ITestOutputHelper output)
{
	private const int StudentCount = 2_000;
	private const int PlanCount = 12_000;
	private const int ExamCount = 40;
	private const int Iterations = 20;
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	[AcademicPerformancePostgresFact]
	public async Task Record_Actual_Http_Queries_And_Postgres_Plans_On_Synthetic_School_Data()
	{
		await using var factory = new AcademicPerformancePostgresTestFactory();
		using var client = factory.CreateClient();
		client.Timeout = TimeSpan.FromMinutes(2);
		var fixture = await SeedAsync(factory);
		await using var connection = new NpgsqlConnection(factory.ConnectionString);
		await connection.OpenAsync();
		await using (var analyze = new NpgsqlCommand("ANALYZE", connection)) await analyze.ExecuteNonQueryAsync();
		var endpoints = new Dictionary<string, string>
		{
			["subjects"] = $"/api/academic-mediums/{factory.SeededMediumId}/subjects",
			["lesson-plans"] = $"/api/academic/curriculum/lesson-plans?AcademicYearId={fixture.AcademicYearId}&MaxResultCount=50",
			["exams"] = $"/api/academics/exams?StandardId={factory.SeededStandardId}&MaxResultCount=20&StartDate=2026-01-01&EndDate=2026-12-31",
			["marks"] = $"/api/academics/exams/marks?ExamId={fixture.ExamId}&SubjectId={fixture.SubjectId}&DivisionId={factory.SeededDivisionId}&PageSize=50",
			["student-360"] = $"/api/academics/student-academic-360/{fixture.StudentId}"
		};
		var artifactDirectory = Environment.GetEnvironmentVariable("ACADEMIC_PERF_OUTPUT")
			?? Path.Combine(FindRepositoryRoot(), ".artifacts", "academic-performance", "http-postgres");
		Directory.CreateDirectory(artifactDirectory);
		var artifactPath = Path.Combine(artifactDirectory, "measurements.json");
		var results = new List<EndpointMeasurement>();
		string? failure = null;
		try
		{
			foreach (var endpoint in endpoints)
				results.Add(await MeasureAsync("current", endpoint.Key, endpoint.Value, client, factory.Metrics, connection));
			if (Environment.GetEnvironmentVariable("ACADEMIC_PERF_COMPARE_INDEXES") == "true")
			{
				// Probes exist only in this guarded disposable database, never in the application model.
				await using var probe = new NpgsqlCommand("""
					CREATE INDEX bench_probe_marks_student ON "academic_ExamMarks" ("TenantId", "StudentId", "ExamId") WHERE NOT "IsDeleted";
					CREATE INDEX bench_probe_marks_division ON "academic_ExamMarks" ("TenantId", "ExamId", "DivisionId", "SubjectId") WHERE NOT "IsDeleted";
					CREATE INDEX bench_probe_marks_subject ON "academic_ExamMarks" ("TenantId", "SubjectId") WHERE NOT "IsDeleted";
					CREATE INDEX bench_probe_lesson_page ON "academic_LessonPlans" ("TenantId", "AcademicYearId", "CreationTime" DESC) WHERE NOT "IsDeleted";
					ANALYZE;
					""", connection);
				await probe.ExecuteNonQueryAsync();
				foreach (var endpoint in endpoints)
					results.Add(await MeasureAsync("temporary-candidate-indexes", endpoint.Key, endpoint.Value, client, factory.Metrics, connection));
			}
			// Exercise the provider-specific keyset cursor after all measurements, so state changes
			// cannot affect the workload being compared. SQLite coverage alone is insufficient.
			using var workerScope = factory.Services.CreateScope();
			var worker = workerScope.ServiceProvider.GetRequiredService<ExamAutoCompletionService>();
			var now = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc);
			(await worker.CompleteDueExamsAsync(now)).ShouldBeGreaterThanOrEqualTo(ExamCount);
			(await worker.CompleteDueExamsAsync(now)).ShouldBe(0);
		}
		catch (Exception exception)
		{
			failure = exception.Message;
			throw;
		}
		finally
		{
			await File.WriteAllTextAsync(artifactPath, JsonSerializer.Serialize(new
			{
				RecordedAtUtc = DateTimeOffset.UtcNow,
				ApplicationModuleId = typeof(ExamAppService).Module.ModuleVersionId,
				Environment = "Loopback PostgreSQL; ASP.NET TestServer; synthetic data; test authentication; auditing/workers disabled",
				Limitations = "Sequential warm requests; allocations are process-wide and include measurement overhead; not production p95 or concurrency evidence. SQL duration stops at reader creation; rows count actual reader consumption.",
				Dataset = new { Students = StudentCount, Subjects = 40, LessonPlans = PlanCount, Exams = ExamCount, Marks = ExamCount * 4 * StudentCount },
				Warmups = 3, Iterations, Failure = failure, Results = results
			}, JsonOptions));
			output.WriteLine($"Measurements and PostgreSQL plans: {artifactPath}");
		}
	}

	private async Task<EndpointMeasurement> MeasureAsync(string phase, string name, string uri, HttpClient client,
		AcademicCommandMetrics metrics, NpgsqlConnection connection)
	{
		for (var i = 0; i < 3; i++)
		{
			using var response = await client.GetAsync(uri);
			var body = await response.Content.ReadAsStringAsync();
			response.IsSuccessStatusCode.ShouldBeTrue($"{name}: {(int)response.StatusCode}: {body}");
			using var document = JsonDocument.Parse(body);
			if (name is "lesson-plans" or "exams" or "marks")
			{
				var expectedCount = name == "lesson-plans" ? PlanCount : name == "exams" ? ExamCount : StudentCount;
				document.RootElement.GetProperty("totalCount").GetInt32().ShouldBeGreaterThanOrEqualTo(expectedCount);
				document.RootElement.GetProperty("items").GetArrayLength().ShouldBeGreaterThan(0);
			}
		}
		var samples = new List<RequestMeasurement>();
		List<AcademicCommandMetrics.CommandMeasurement> commands = [];
		for (var i = 0; i < Iterations; i++)
		{
			metrics.Begin();
			var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
			var watch = Stopwatch.StartNew();
			using var response = await client.GetAsync(uri);
			var body = await response.Content.ReadAsByteArrayAsync();
			watch.Stop();
			var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
			commands = metrics.End();
			response.IsSuccessStatusCode.ShouldBeTrue(name);
			commands.Count.ShouldBeGreaterThan(0, "The command interceptor must actually observe the request.");
			samples.Add(new(watch.Elapsed.TotalMilliseconds, commands.Count, commands.Sum(command => command.RowsRead),
				commands.Sum(command => command.ExecuteMilliseconds), allocatedBytes, body.Length));
		}
		var plans = new List<object>();
		foreach (var captured in commands.Where(command => command.Sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)))
		{
			await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + captured.Sql, connection);
			foreach (var parameter in captured.Parameters) command.Parameters.Add(parameter.Clone());
			var planText = (string)(await command.ExecuteScalarAsync())!;
			plans.Add(new
			{
				captured.Sql,
				Parameters = captured.Parameters.Select(parameter => new { parameter.ParameterName, parameter.NpgsqlDbType, parameter.Value }).ToArray(),
				captured.RowsRead,
				Plan = JsonSerializer.Deserialize<JsonElement>(planText)
			});
		}
		var elapsed = samples.Select(sample => sample.ElapsedMilliseconds).Order().ToArray();
		var result = new EndpointMeasurement(phase, name, uri, elapsed[Iterations / 2], elapsed[(int)Math.Ceiling(Iterations * .95) - 1],
			samples.Average(sample => sample.AllocatedBytes), samples.Average(sample => sample.SqlCommands),
			samples.Average(sample => sample.RowsRead), samples, plans);
		output.WriteLine($"{name}: p50={result.P50Milliseconds:F2}ms p95={result.P95Milliseconds:F2}ms SQL={result.MeanSqlCommands:F0} rows={result.MeanRowsRead:F0}");
		return result;
	}

	private static async Task<Fixture> SeedAsync(AcademicPerformancePostgresTestFactory factory)
	{
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var studentIds = new List<Guid>();
		var examIds = new List<Guid>();
		var subjectIds = new List<Guid>();
		Guid academicYearId;
		using (var scope = factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
		{
			var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			academicYearId = await db.Set<EducationBoard>().Where(board => board.Id == factory.SeededBoardId)
				.Select(board => board.AcademicYearId).SingleAsync();
			var teacherId = await db.Set<Employee>().Select(employee => employee.Id).FirstAsync();
			for (var i = 0; i < 40; i++)
			{
				var subject = new Subject(Guid.NewGuid(), tenantId, $"Benchmark subject {i:D3}", "#112233", factory.SeededMediumId);
				subjectIds.Add(subject.Id);
				db.Add(subject);
			}
			var examType = new ExamType(Guid.NewGuid(), tenantId, "Benchmark examination", null, factory.SeededMediumId);
			db.Add(examType);
			for (var i = 0; i < ExamCount; i++)
			{
				var exam = new Exam(Guid.NewGuid(), examType.Id, $"Benchmark exam {i:D3}", factory.SeededStandardId, tenantId);
				exam.AddDivision(Guid.NewGuid(), factory.SeededDivisionId);
				exam.ChangeStatus(ExamStatus.Published);
				foreach (var subjectId in subjectIds.Take(4))
					exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subjectId, new DateTime(2026, 8, 1),
						new DateTime(2026, 8, 1, 9, 0, 0), new DateTime(2026, 8, 1, 10, 0, 0), 100, 35));
				examIds.Add(exam.Id);
				db.Add(exam);
			}
			await db.SaveChangesAsync();
			db.ChangeTracker.Clear();
			for (var i = 1; i <= StudentCount; i++)
			{
				var student = new Student(Guid.NewGuid(), tenantId, $"Benchmark{i:D5}", null, "Student",
					academicYearId, factory.SeededBoardId, factory.SeededMediumId, factory.SeededMediumStageId,
					factory.SeededStandardId, factory.SeededDivisionId, i, i, StudentLifeCycleStatusEnum.Admission);
				student.ApproveAdmission();
				studentIds.Add(student.Id);
				db.Add(student);
				if (i % 500 == 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
			}
			for (var i = 1; i <= PlanCount; i++)
			{
				db.Add(new LessonPlan(Guid.NewGuid(), tenantId, academicYearId, teacherId, factory.SeededStandardId,
					factory.SeededDivisionId, subjectIds[i % subjectIds.Count], Guid.NewGuid(),
					new DateTime(2026, 1, 1).AddDays(i % 240), $"Benchmark lesson {i:D5}", i % 101));
				if (i % 500 == 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
			}
			await uow.CompleteAsync();
		}
		await using var connection = new NpgsqlConnection(factory.ConnectionString);
		await connection.OpenAsync();
		await using (var writer = await connection.BeginBinaryImportAsync("""
			COPY "academic_ExamMarks" ("Id", "TenantId", "ExamId", "StudentId", "SubjectId", "DivisionId", "MarksObtained", "IsAbsent", "AttendanceStatus", "IsPublished", "CreationTime", "IsDeleted") FROM STDIN (FORMAT BINARY)
			"""))
		{
			foreach (var examId in examIds)
			foreach (var subjectId in subjectIds.Take(4))
			foreach (var studentId in studentIds)
			{
				await writer.StartRowAsync();
				await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
				await writer.WriteAsync(tenantId, NpgsqlDbType.Uuid);
				await writer.WriteAsync(examId, NpgsqlDbType.Uuid);
				await writer.WriteAsync(studentId, NpgsqlDbType.Uuid);
				await writer.WriteAsync(subjectId, NpgsqlDbType.Uuid);
				await writer.WriteAsync(factory.SeededDivisionId, NpgsqlDbType.Uuid);
				await writer.WriteAsync(75d, NpgsqlDbType.Double);
				await writer.WriteAsync(false, NpgsqlDbType.Boolean);
				await writer.WriteAsync(0, NpgsqlDbType.Integer);
				await writer.WriteAsync(true, NpgsqlDbType.Boolean);
				await writer.WriteAsync(DateTime.UtcNow, NpgsqlDbType.Timestamp);
				await writer.WriteAsync(false, NpgsqlDbType.Boolean);
			}
			await writer.CompleteAsync();
		}
		return new(academicYearId, examIds[0], subjectIds[0], studentIds[0]);
	}

	private static string FindRepositoryRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
			if (Directory.Exists(Path.Combine(directory.FullName, "src", "ShikshaMitra.HttpApi.Host"))) return directory.FullName;
		throw new InvalidOperationException("Set ACADEMIC_PERF_OUTPUT when running outside the repository.");
	}

	private sealed record Fixture(Guid AcademicYearId, Guid ExamId, Guid SubjectId, Guid StudentId);
	private sealed record RequestMeasurement(double ElapsedMilliseconds, int SqlCommands, long RowsRead, double SqlExecuteMilliseconds, long AllocatedBytes, int ResponseBytes);
	private sealed record EndpointMeasurement(string Phase, string Name, string Uri, double P50Milliseconds, double P95Milliseconds,
		double MeanAllocatedBytes, double MeanSqlCommands, double MeanRowsRead, List<RequestMeasurement> Samples, List<object> QueryPlans);
}
