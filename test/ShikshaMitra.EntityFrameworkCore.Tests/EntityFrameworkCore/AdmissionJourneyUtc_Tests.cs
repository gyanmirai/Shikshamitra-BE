using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class AdmissionJourneyUtc_Tests
{
	[Fact]
	public void Sqlite_keeps_DateTime_mapping_for_server_side_date_aggregates()
	{
		var builder = new ModelBuilder(new ConventionSet());
		builder.SetDatabaseProvider(EfCoreDatabaseProvider.Sqlite);
		builder.Entity<AdmissionApplicationStageInstance>().Property(x => x.EnteredAt);
		((IEntityTypeConfiguration<AdmissionApplicationStageInstance>)new AdmissionApplicationConfiguration())
			.Configure(builder.Entity<AdmissionApplicationStageInstance>());
		var property = builder.Model.FindEntityType(typeof(AdmissionApplicationStageInstance))!
			.FindProperty(nameof(AdmissionApplicationStageInstance.EnteredAt))!;
		var converter = property.GetValueConverter().ShouldNotBeNull();
		// SQLite supports DateTime aggregates; UTC normalization must not change the provider type to DateTimeOffset.
		converter.ModelClrType.ShouldBe(typeof(DateTime));
		converter.ProviderClrType.ShouldBe(typeof(DateTime));
		var instant = new DateTime(2026, 10, 28, 23, 45, 0, DateTimeKind.Utc);
		foreach (var input in new[] { instant, DateTime.SpecifyKind(instant, DateTimeKind.Unspecified), instant.ToLocalTime() })
		{
			var stored = (DateTime)converter.ConvertToProvider(input)!;
			stored.ShouldBe(instant);
			stored.Kind.ShouldBe(DateTimeKind.Utc);
			var loaded = (DateTime)converter.ConvertFromProvider(DateTime.SpecifyKind(stored, DateTimeKind.Unspecified))!;
			loaded.ShouldBe(instant);
			loaded.Kind.ShouldBe(DateTimeKind.Utc);
		}
		converter.ConvertToProvider(null).ShouldBeNull();
		converter.ConvertFromProvider(null).ShouldBeNull();
	}

	[Fact]
	public async Task Sqlite_executes_stage_date_aggregates_in_SQL_and_reloads_UTC_instants()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		var options = new DbContextOptionsBuilder<StageDateContext>().UseSqlite(connection).Options;
		await using var db = new StageDateContext(options);
		await db.Database.EnsureCreatedAsync();
		var workflowId = Guid.NewGuid();
		var pendingWorkflowId = Guid.NewGuid();
		var first = new DateTime(2026, 10, 28, 23, 45, 0, DateTimeKind.Utc);
		var last = first.AddHours(4);
		foreach (var enteredAt in new DateTime?[]
		{
			first, DateTime.SpecifyKind(first.AddHours(2), DateTimeKind.Unspecified), last.ToLocalTime(), null
		})
		{
			var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), null, workflowId,
				Guid.NewGuid(), "Assessment", "Assessment", AdmissionWorkflowStageType.Assessment, 1, false);
			db.Add(stage);
			db.Entry(stage).Property(x => x.EnteredAt).CurrentValue = enteredAt;
		}
		db.Add(new AdmissionApplicationStageInstance(Guid.NewGuid(), null, pendingWorkflowId,
			Guid.NewGuid(), "Assessment", "Assessment", AdmissionWorkflowStageType.Assessment, 1, false));
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		var query = db.Set<AdmissionApplicationStageInstance>().GroupBy(x => x.WorkflowInstanceId)
			.Select(group => new
			{
				WorkflowId = group.Key,
				FirstEnteredAt = group.Min(x => x.EnteredAt),
				LastEnteredAt = group.Max(x => x.EnteredAt),
				EnteredCount = group.Count(x => x.EnteredAt.HasValue)
			});
		var sql = query.ToQueryString();
		sql.ShouldContain("MIN(");
		sql.ShouldContain("MAX(");
		sql.ShouldContain("GROUP BY");
		var rows = await query.ToListAsync();
		var entered = rows.Single(x => x.WorkflowId == workflowId);
		entered.FirstEnteredAt.ShouldBe(first);
		entered.FirstEnteredAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
		entered.LastEnteredAt.ShouldBe(last);
		entered.LastEnteredAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
		entered.EnteredCount.ShouldBe(3);
		var pending = rows.Single(x => x.WorkflowId == pendingWorkflowId);
		pending.FirstEnteredAt.ShouldBeNull();
		pending.LastEnteredAt.ShouldBeNull();
		pending.EnteredCount.ShouldBe(0);
		var reloaded = await db.Set<AdmissionApplicationStageInstance>().AsNoTracking()
			.Where(x => x.EnteredAt.HasValue).OrderBy(x => x.EnteredAt).ToListAsync();
		reloaded.Select(x => x.EnteredAt).ShouldBe(new DateTime?[] { first, first.AddHours(2), last });
		reloaded.ShouldAllBe(x => x.EnteredAt!.Value.Kind == DateTimeKind.Utc);
	}

	[Fact]
	public void Application_and_workflow_timestamps_preserve_instants_under_legacy_Npgsql()
	{
		var configuration = new AdmissionApplicationConfiguration();
		Verify<AdmissionApplication>(configuration, "DecisionAt", "FinalizedAt", "LastSavedAt", "SubmittedAt", "OfferAcceptedAt");
		Verify<AdmissionApplicationWorkflowInstance>(configuration, "StartedAt", "CompletedAt");
		Verify<AdmissionApplicationStageInstance>(configuration, "EnteredAt", "CompletedAt");
		Verify<AdmissionWorkflowTransitionHistory>(configuration, "OccurredAt");
		Verify<AdmissionFormInstance>(configuration, "StartedAt", "LastSavedAt", "SubmittedAt");
		Verify<AdmissionFormFieldValue>(configuration, "LastModifiedAt");
		Verify<AdmissionFormSubmission>(configuration, "SubmittedAt");
		Verify<AdmissionFormChangeHistory>(configuration, "OccurredAt");
		Verify<AdmissionActivity>(configuration, "OccurredAt");
		Verify<AdmissionApplicationDecisionHistory>(configuration, "OccurredAt");
	}

	[Fact]
	public void Cycle_schedules_lifecycle_and_history_preserve_instants_under_legacy_Npgsql()
	{
		Verify<AdmissionCycle>(new AdmissionCycleConfiguration(), "ScheduledPublishAt", "ScheduledCloseAt",
			"PublishedAt", "ClosedAt", "ReopenedAt", "ArchivedAt");
		Verify<AdmissionCycleHistory>(new AdmissionCycleHistoryConfiguration(), "OccurredAt");
	}

	[Fact]
	public void Document_evidence_and_review_times_preserve_the_recorded_instant()
	{
		var configuration = new AdmissionDocumentConfiguration();
		Verify<AdmissionDocumentEvidence>(configuration, "SubmittedAt", "OriginalSeenAt", "StudentLinkedAt");
		Verify<AdmissionDocumentReview>(configuration, "ReviewedAt");
	}

	[Fact]
	public void Onboarding_deadlines_seats_identifiers_and_completion_preserve_instants()
	{
		var configuration = new StudentOnboardingConfiguration();
		Verify<StudentOnboardingCase>(configuration, "GuardianContactVerifiedAt", "DueAt", "StartedAt", "ReadyAt", "CompletedAt", "CancelledAt");
		Verify<StudentOnboardingStep>(configuration, "CompletedAt");
		Verify<StudentSeatReservation>(configuration, "ReservedAt", "ExpiresAt", "CommittedAt", "ReleasedAt");
		Verify<StudentIdentifierAllocation>(configuration, "AllocatedAt", "CommittedAt");
		Verify<StudentOnboardingServiceHandoff>(configuration, "LastAttemptAt", "AcknowledgedAt");
	}

	private sealed class StageDateContext(DbContextOptions<StageDateContext> options) : DbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder builder)
		{
			builder.SetDatabaseProvider(EfCoreDatabaseProvider.Sqlite);
			((IEntityTypeConfiguration<AdmissionApplicationStageInstance>)new AdmissionApplicationConfiguration())
				.Configure(builder.Entity<AdmissionApplicationStageInstance>());
			// This fixture exercises the real stage mapping without unrelated parent tables.
			builder.Ignore<AdmissionApplicationWorkflowInstance>();
			builder.Ignore<AdmissionWorkflowStageDefinition>();
		}
	}

	private static void Verify<TEntity>(IEntityTypeConfiguration<TEntity> configuration, params string[] properties)
		where TEntity : class
	{
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
		var builder = new ModelBuilder(new ConventionSet());
		builder.SetDatabaseProvider(EfCoreDatabaseProvider.PostgreSql);
		foreach (var name in properties)
			builder.Entity<TEntity>().Property(typeof(TEntity).GetProperty(name)!.PropertyType, name);
		configuration.Configure(builder.Entity<TEntity>());
		using var context = new DbContext(new DbContextOptionsBuilder()
			.UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
		var mapping = context.GetService<IRelationalTypeMappingSource>()
			.FindMapping(typeof(DateTimeOffset), "timestamp with time zone")!;
		var instant = new DateTime(2026, 10, 12, 7, 26, 0, DateTimeKind.Utc);
		foreach (var name in properties)
		{
			var property = builder.Model.FindEntityType(typeof(TEntity))!.FindProperty(name)!;
			property.GetColumnType().ShouldBe("timestamp with time zone");
			var converter = property.GetValueConverter()!;
			converter.ProviderClrType.ShouldBe(typeof(DateTimeOffset));
			foreach (var input in new[] { instant, DateTime.SpecifyKind(instant, DateTimeKind.Unspecified), instant.ToLocalTime() })
			{
				var stored = (DateTimeOffset)converter.ConvertToProvider(input)!;
				stored.Offset.ShouldBe(TimeSpan.Zero);
				stored.UtcDateTime.ShouldBe(instant);
				using var command = new NpgsqlCommand();
				((NpgsqlParameter)mapping.CreateParameter(command, name, stored)).NpgsqlDbType
					.ShouldBe(NpgsqlDbType.TimestampTz);
				var loaded = (DateTime)converter.ConvertFromProvider(stored.ToOffset(TimeSpan.FromHours(5.5)))!;
				loaded.Kind.ShouldBe(DateTimeKind.Utc);
				loaded.ShouldBe(instant);
			}
			if (Nullable.GetUnderlyingType(property.ClrType) != null)
			{
				converter.ConvertToProvider(null).ShouldBeNull();
				converter.ConvertFromProvider(null).ShouldBeNull();
			}
		}
	}
}
