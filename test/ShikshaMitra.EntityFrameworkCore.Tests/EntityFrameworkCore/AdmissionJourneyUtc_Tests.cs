using System;
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
		builder.Model.FindEntityType(typeof(AdmissionApplicationStageInstance))!
			.FindProperty("EnteredAt")!.GetValueConverter().ShouldBeNull();
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

	private static void Verify<TEntity>(IEntityTypeConfiguration<TEntity> configuration, params string[] properties)
		where TEntity : class
	{
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
		var builder = new ModelBuilder(new ConventionSet());
		builder.SetDatabaseProvider(EfCoreDatabaseProvider.PostgreSql);
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
