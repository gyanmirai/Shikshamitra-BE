using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Linq;
using Volo.Abp.Users;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed class AssessmentRosterPostgresFactAttribute : FactAttribute
{
	public AssessmentRosterPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASSESSMENT_ROSTER_TEST_POSTGRES")))
			Skip = "Requires a fresh loopback admissions_assessment_tests_* database name in ASSESSMENT_ROSTER_TEST_POSTGRES.";
	}
}

/// <summary>
/// Runs the production roster/read/access query services against real Npgsql/EF. The isolated admission-only model
/// has no identity/grant tables or school data. Scope/permission collaborators are injected; authorization itself
/// remains covered by the ABP integration suite. This is query scale evidence, not a production load/SLA test.
/// </summary>
public class AdmissionAssessmentRosterPostgres_Tests(ITestOutputHelper output)
{
	[AssessmentRosterPostgresFact]
	public async Task Assessment_roster_Postgres_scales_without_N_plus_one_and_keeps_page_counts_sort_and_history_correct()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ASSESSMENT_ROSTER_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^admissions_assessment_tests_[a-z0-9_]{1,32}$"))
			throw new InvalidOperationException("Only a fresh disposable loopback admissions_assessment_tests_* database is permitted.");
		var databaseName = connection.Database;
		var adminConnection = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres", Pooling = false };
		await using var admin = new NpgsqlConnection(adminConnection.ConnectionString);
		await admin.OpenAsync();
		await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", admin))
		{
			exists.Parameters.AddWithValue("name", databaseName);
			Convert.ToInt64(await exists.ExecuteScalarAsync()).ShouldBe(0, "Never reset, reuse or delete an existing database.");
		}
		await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
		try
		{
			var commands = new CommandCounter();
			await using var db = new RosterQueryDb(new DbContextOptionsBuilder<RosterQueryDb>().UseNpgsql(connection.ConnectionString)
				.AddInterceptors(commands).Options);
			await db.Database.EnsureCreatedAsync();
			await db.Database.OpenConnectionAsync();
			output.WriteLine($"PostgreSQL server {db.Database.GetDbConnection().ServerVersion}; EF/Npgsql {typeof(NpgsqlConnection).Assembly.GetName().Version}");
			var fixture = new RosterFixture(db);
			foreach (var population in new[] { 100, 1000, 5000 })
			{
				var scope = await fixture.SeedAsync(population);
				var input = new AdmissionAssessmentRosterInput { AdmissionOfferingId = scope.OfferingId,
					WorkflowStageDefinitionId = scope.DefinitionId, MaxResultCount = 25, Sorting = "applicationNumber desc", SkipCount = population - 25 };
				await fixture.Service.GetAsync(input); // Warm translation/model caches before timed observations.
				commands.Reset();
				fixture.HistoryReadCount = 0;
				var watch = Stopwatch.StartNew();
				var result = await fixture.Service.GetAsync(input);
				watch.Stop();
				result.TotalCount.ShouldBe(population);
				result.Counts.All.ShouldBe(population);
				new[] { result.Counts.ToBook, result.Counts.Attendance, result.Counts.Result, result.Counts.Review,
					result.Counts.Ready, result.Counts.Attention, result.Counts.Completed }
					.ShouldBe(Enumerable.Range(0, 7).Select(queue => Enumerable.Range(0, population).Count(i => i % 7 == queue)).ToArray());
				result.Items.Select(row => row.ApplicationNumber).ShouldBe(Enumerable.Range(0, 25).Reverse()
					.Select(i => $"SYN-{population:D5}-{i:D5}"));
				result.Items.Count.ShouldBe(25);
				fixture.HistoryReadCount.ShouldBeLessThanOrEqualTo(25 * 5, "Only the selected page expands complete attempt histories.");
				commands.Count.ShouldBeLessThanOrEqualTo(14, "SQL round trips must remain independent of applicant population.");
				output.WriteLine($"Npgsql PostgreSQL; candidates={population}; warm roster={watch.Elapsed.TotalMilliseconds:F1}ms; SQL commands={commands.Count}; full history rows={fixture.HistoryReadCount}");
				input.Queue = "review";
				input.SkipCount = 0;
				result = await fixture.Service.GetAsync(input);
				result.TotalCount.ShouldBe(Enumerable.Range(0, population).Count(i => i % 7 == 3));
				result.Counts.All.ShouldBe(population);
				result.Items.ShouldAllBe(row => row.Queue == "review" && !row.Assessment.CanReview);
				input.Queue = null;
				input.Search = "SYN-" + population.ToString("D5") + "-00000";
				result = await fixture.Service.GetAsync(input);
				result.TotalCount.ShouldBe(1);
				result.Counts.ToBook.ShouldBe(1);
				result.Items.Single().Assessment.Attempts.Count.ShouldBe(4, "Cancelled history is preserved, but does not consume the attempt allowance.");
				input.Search = null;
				input.Sorting = "sessionStartsAt desc";
				result = await fixture.Service.GetAsync(input);
				result.Items.ShouldAllBe(row => row.Assessment.Attempts.Last().SessionId == scope.LaterSessionId);
				input.SessionId = scope.SessionId;
				result = await fixture.Service.GetAsync(input);
				var booked = Enumerable.Range(0, population).Count(i => i % 7 != 0 && i % 2 == 0);
				result.Counts.All.ShouldBe(booked);
				result.Items.ShouldAllBe(row => row.Assessment.Attempts.Last().SessionId == scope.SessionId);
				output.WriteLine($"population={population}: counts, late page ordering, queue/search/session scope and complete page history verified.");
			}
			output.WriteLine("Fixture created no identity users or permission grants; production school database was never used.");
		}
		finally
		{
			NpgsqlConnection.ClearAllPools();
			// Only the validated database created by this test is removed. Existing databases never enter this block.
			await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
			await drop.ExecuteNonQueryAsync();
		}
	}

	private sealed class RosterFixture
	{
		private readonly RosterQueryDb db;
		private readonly Guid userId = Guid.NewGuid();
		public AdmissionAssessmentRosterService Service { get; }
		public int HistoryReadCount { get; set; }

		public RosterFixture(RosterQueryDb database)
		{
			db = database;
			var applications = Repository<AdmissionApplication>();
			var stages = Repository<AdmissionApplicationStageInstance>();
			var instances = Repository<AdmissionApplicationWorkflowInstance>();
			var attempts = Repository<AdmissionAssessmentAttempt>();
			var sessions = Repository<AdmissionAssessmentSession>();
			var transitions = Repository<AdmissionWorkflowTransitionDefinition>();
			var scope = Substitute.For<IUserScopeAccessService>();
			scope.GetCurrentUserScopeAsync().Returns(new UserScopeContext { IsAdmin = true, TenantId = db.TenantId, UserId = userId });
			var access = new AdmissionAssessmentAccessService(applications, stages, instances, Repository<AdmissionWorkflowStageDefinition>(),
				Repository<AdmissionWorkflowDefinition>(), Repository<AdmissionOffering>(), Substitute.For<IRepository<AdmissionCycle, Guid>>(),
				Substitute.For<IRepository<Division, Guid>>(), scope);
			var runtime = new AdmissionWorkflowRuntimeValueService(Repository<AdmissionFormInstance>(), Repository<AdmissionFormFieldValue>(),
				Substitute.For<IRepository<AdmissionInquiry, Guid>>(), new AdmissionAssessmentRuntimeService(attempts, access));
			var permissions = Substitute.For<IPermissionChecker>();
			permissions.IsGrantedAsync(Arg.Any<string>()).Returns(true);
			var reader = new AdmissionAssessmentReadService(attempts, sessions, transitions, permissions, runtime,
				new AdmissionWorkflowTransitionSelector(new AdmissionWorkflowConditionEvaluator()), access);
			var currentUser = Substitute.For<ICurrentUser>();
			currentUser.Id.Returns(userId);
			var tenant = Substitute.For<Volo.Abp.MultiTenancy.ICurrentTenant>();
			tenant.Id.Returns(db.TenantId);
			var cycleRepository = Substitute.For<IRepository<AdmissionCycle, Guid>>();
			cycleRepository.GetQueryableAsync().Returns(Task.FromResult(Array.Empty<AdmissionCycle>().AsQueryable()));
			var divisionRepository = Substitute.For<IRepository<Division, Guid>>();
			divisionRepository.GetQueryableAsync().Returns(Task.FromResult(Array.Empty<Division>().AsQueryable()));
			var placement = new AdmissionApplicationPlacementScopeService(scope, null!, null!, null!, null!, null!, null!);
			var applicationScope = new AdmissionApplicationScopeAccessService(Repository<AdmissionOffering>(), cycleRepository,
				divisionRepository, scope, applications, placement);
			var provider = new ServiceCollection().AddSingleton<IAsyncQueryableExecuter>(new AsyncQueryableExecuter([new EfCoreAsyncQueryableProvider()]))
				.AddSingleton(currentUser).AddSingleton(tenant).AddSingleton(applicationScope).BuildServiceProvider();
			Service = new(Repository<AdmissionApplicant>(), instances, stages, attempts, sessions, transitions, runtime, access, reader);
			foreach (var service in new ApplicationService[] { Service, access, reader, applicationScope, placement })
				service.LazyServiceProvider = new AbpLazyServiceProvider(provider);
		}

		private IRepository<T, Guid> Repository<T>() where T : class, IEntity<Guid>
		{
			var repository = Substitute.For<IRepository<T, Guid>>();
			repository.AsyncExecuter.Returns(new AsyncQueryableExecuter([new EfCoreAsyncQueryableProvider()]));
			repository.GetQueryableAsync().Returns(_ => Task.FromResult(db.Set<T>().AsNoTracking().AsQueryable()));
			repository.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
				db.Set<T>().AsNoTracking().FirstOrDefaultAsync(row => row.Id == call.Arg<Guid>()));
			repository.GetListAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(async call =>
			{
				var rows = await db.Set<T>().AsNoTracking().Where(call.Arg<Expression<Func<T, bool>>>()).ToListAsync();
				if (typeof(T) == typeof(AdmissionAssessmentAttempt)) HistoryReadCount += rows.Count;
				return rows;
			});
			return repository;
		}

		public async Task<(Guid OfferingId, Guid DefinitionId, Guid SessionId, Guid LaterSessionId)> SeedAsync(int count)
		{
			var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), db.TenantId, "Synthetic scale", "SCALE");
			workflow.Publish(userId);
			var policy = new AdmissionAssessmentPolicy { Version = 1, Enabled = true, AttendanceRequired = true,
				ScoringEnabled = true, TotalMarks = 100, PassingMarks = 40, MaxAttempts = 10 };
			var definition = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "ASSESS", "Assessment", AdmissionWorkflowStageType.Assessment,
				0, true, false, null, JsonSerializer.Serialize(new { assessment = policy }), true, false);
			var next = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "NEXT", "Next", AdmissionWorkflowStageType.Custom,
				1, true, false, null, null, false, true);
			var route = new AdmissionWorkflowTransitionDefinition(Guid.NewGuid(), workflow.Id, definition.Id, next.Id, 1, """
				{"operator":"ALL","conditions":[
				{"field":"assessment.outcome","operator":"Equals","value":"passed"},
				{"field":"application.eligibility","operator":"Equals","value":"eligible"},
				{"field":"application.status","operator":"Equals","value":"Draft"}]}
				""");
			var offering = new AdmissionOffering(Guid.NewGuid(), db.TenantId, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
				Guid.NewGuid(), Guid.NewGuid(), "SCALE", true, null, null);
			offering.ConfigureReferences(workflow.Id, null, null);
			var session = new AdmissionAssessmentSession(Guid.NewGuid(), db.TenantId, offering.Id, definition.Id, "Synthetic session",
				DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), "Synthetic room", 10000);
			var laterSession = new AdmissionAssessmentSession(Guid.NewGuid(), db.TenantId, offering.Id, definition.Id, "Later synthetic session",
				DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-10), "Synthetic room B", 10000);
			db.AddRange(workflow, definition, next, route, offering, session, laterSession);
			for (var i = 0; i < count; i++)
			{
				var applicant = new AdmissionApplicant(Guid.NewGuid(), db.TenantId, Guid.NewGuid(), $"Synthetic {i:D5}", null,
					"Applicant", null, null, null, Guid.NewGuid());
				var application = new AdmissionApplication(Guid.NewGuid(), db.TenantId, $"SYN-{count:D5}-{i:D5}", applicant.Id,
					null, offering.AdmissionCycleId, offering.Id, workflow.Id, Guid.NewGuid());
				var instance = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), db.TenantId, application.Id, workflow.Id);
				var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), db.TenantId, instance.Id, definition.Id, "ASSESS", "Assessment",
					AdmissionWorkflowStageType.Assessment, 0, true);
				application.SetCurrentStage(stage.Id);
				db.AddRange(applicant, application, instance, stage);
				var form = new AdmissionFormInstance(Guid.NewGuid(), db.TenantId, application.Id, application.FormDefinitionId, 1);
				db.AddRange(form,
					new AdmissionFormFieldValue(Guid.NewGuid(), db.TenantId, form.Id, "eligibility", "\"eligible\""),
					new AdmissionFormFieldValue(Guid.NewGuid(), db.TenantId, form.Id, "application.status", "\"Approved\""),
					new AdmissionFormFieldValue(Guid.NewGuid(), db.TenantId, form.Id, "assessment.outcome", "\"passed\""));
				for (var number = 1; number <= 4; number++)
				{
					var cancelled = new AdmissionAssessmentAttempt(Guid.NewGuid(), db.TenantId, application.Id, stage.Id, session.Id, number, "{}");
					cancelled.Cancel("Synthetic cancelled history");
					db.Add(cancelled);
				}
				if (i % 7 != 0)
				{
					var attempt = new AdmissionAssessmentAttempt(Guid.NewGuid(), db.TenantId, application.Id, stage.Id,
						i % 2 == 0 ? session.Id : laterSession.Id, 5, "{}");
					if (i % 7 >= 2) attempt.RecordAttendance("present", null, userId, policy);
					if (i % 7 >= 3) attempt.RecordResult(i % 7 == 5 ? 10 : 80, null, null, userId, policy with { ReviewRequired = i % 7 == 3 });
					db.Add(attempt);
				}
				if (i % 7 == 6) { stage.Complete(userId); application.CompleteWorkflow(); instance.Complete(); }
			}
			// Plain EF test context has no ABP audit interceptor. Give creation timestamps an explicit UTC kind.
			foreach (var entry in db.ChangeTracker.Entries())
				if (entry.Metadata.FindProperty("CreationTime") != null) entry.Property("CreationTime").CurrentValue = DateTime.UtcNow;
			await db.SaveChangesAsync();
			db.ChangeTracker.Clear();
			return (offering.Id, definition.Id, session.Id, laterSession.Id);
		}
	}

	private sealed class RosterQueryDb(DbContextOptions<RosterQueryDb> options) : DbContext(options)
	{
		public Guid TenantId { get; } = Guid.NewGuid();
		protected override void OnModelCreating(ModelBuilder model)
		{
			foreach (var type in new[] { typeof(AdmissionApplication), typeof(AdmissionApplicant), typeof(AdmissionApplicationWorkflowInstance),
				typeof(AdmissionApplicationStageInstance), typeof(AdmissionAssessmentAttempt), typeof(AdmissionAssessmentSession),
				typeof(AdmissionWorkflowStageDefinition), typeof(AdmissionWorkflowDefinition), typeof(AdmissionOffering),
				typeof(AdmissionWorkflowTransitionDefinition), typeof(AdmissionFormInstance), typeof(AdmissionFormFieldValue) })
			{
				var entity = model.Entity(type);
				entity.HasKey("Id");
				entity.Ignore("ExtraProperties");
				// Other ABP tests configure Npgsql legacy timestamps process-wide. Keep this UTC fixture's
				// schema explicit so its mapping does not depend on another test class's initialization order.
				foreach (var property in entity.Metadata.GetProperties().Where(property =>
					property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)).ToList())
					entity.Property(property.Name).HasColumnType("timestamp with time zone");
			}
			model.Entity<AdmissionApplication>().HasQueryFilter(row => row.TenantId == TenantId && !row.IsDeleted);
			model.Entity<AdmissionApplicant>().HasQueryFilter(row => row.TenantId == TenantId && !row.IsDeleted);
			model.Entity<AdmissionApplicationWorkflowInstance>().HasQueryFilter(row => row.TenantId == TenantId && !row.IsDeleted);
			model.Entity<AdmissionApplicationStageInstance>().HasQueryFilter(row => row.TenantId == TenantId);
			model.Entity<AdmissionAssessmentAttempt>().HasQueryFilter(row => row.TenantId == TenantId && !row.IsDeleted);
			model.Entity<AdmissionAssessmentSession>().HasQueryFilter(row => row.TenantId == TenantId && !row.IsDeleted);
			model.Entity<AdmissionOffering>().HasQueryFilter(row => row.TenantId == TenantId && !row.IsDeleted);
			model.Entity<AdmissionFormInstance>().HasQueryFilter(row => row.TenantId == TenantId && !row.IsDeleted);
			model.Entity<AdmissionFormFieldValue>().HasQueryFilter(row => row.TenantId == TenantId);
			// Match the production lookup indexes used by correlated latest-attempt selection.
			model.Entity<AdmissionAssessmentAttempt>().HasIndex(row => new { row.StageInstanceId, row.AttemptNumber }).IsUnique();
			model.Entity<AdmissionAssessmentAttempt>().HasIndex(row => new { row.TenantId, row.ApplicationId, row.StageInstanceId });
			model.Entity<AdmissionApplication>().HasIndex(row => new { row.TenantId, row.AdmissionOfferingId, row.Status });
			model.Entity<AdmissionApplication>().HasIndex(row => new { row.TenantId, row.ApplicationNumber }).IsUnique();
			model.Entity<AdmissionApplicationWorkflowInstance>().HasIndex(row => new { row.TenantId, row.ApplicationId }).IsUnique();
			model.Entity<AdmissionApplicationStageInstance>().HasIndex(row => new { row.TenantId, row.WorkflowInstanceId, row.DisplayOrder });
			model.Entity<AdmissionFormInstance>().HasIndex(row => new { row.TenantId, row.ApplicationId }).IsUnique();
			model.Entity<AdmissionFormFieldValue>().HasIndex(row => new { row.FormInstanceId, row.FieldCode }).IsUnique();
		}
	}

	private sealed class CommandCounter : DbCommandInterceptor
	{
		public int Count { get; private set; }
		public void Reset() => Count = 0;
		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
			InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
		{ Count++; return ValueTask.FromResult(result); }
		public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
			InterceptionResult<object> result, CancellationToken cancellationToken = default)
		{ Count++; return ValueTask.FromResult(result); }
	}
}
