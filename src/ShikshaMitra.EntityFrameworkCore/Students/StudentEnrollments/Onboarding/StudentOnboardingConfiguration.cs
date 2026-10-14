using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingConfiguration :
	IEntityTypeConfiguration<StudentOnboardingCase>,
	IEntityTypeConfiguration<StudentOnboardingStep>,
	IEntityTypeConfiguration<StudentOnboardingEvent>,
	IEntityTypeConfiguration<StudentOnboardingPolicy>,
	IEntityTypeConfiguration<StudentOnboardingSettings>,
	IEntityTypeConfiguration<StudentSeatReservation>,
	IEntityTypeConfiguration<StudentIdentifierSeries>,
	IEntityTypeConfiguration<StudentIdentifierAllocation>,
	IEntityTypeConfiguration<StudentOnboardingServiceHandoff>
{
	public void Configure(EntityTypeBuilder<StudentOnboardingCase> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentOnboardingCases", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(StudentOnboardingCase.GuardianContactVerifiedAt),
			nameof(StudentOnboardingCase.DueAt), nameof(StudentOnboardingCase.StartedAt),
			nameof(StudentOnboardingCase.ReadyAt), nameof(StudentOnboardingCase.CompletedAt),
			nameof(StudentOnboardingCase.CancelledAt));
		builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.ActivationIdempotencyKey).HasMaxLength(128);
		// Application services always provide the policy-derived deadline. This portable fallback keeps
		// direct inserts valid in both PostgreSQL production and the SQLite integration-test provider.
		builder.Property(x => x.DueAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
		builder.Property(x => x.CancellationReason).HasMaxLength(1000);
		builder.Property(x => x.LastError).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ApplicationId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.ActivationIdempotencyKey }).IsUnique()
			.HasFilter("\"ActivationIdempotencyKey\" IS NOT NULL AND \"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.AcademicYearId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"Status\" IN (0, 1, 2, 3, 6)");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DivisionId, x.RollNumber })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"DivisionId\" IS NOT NULL AND \"RollNumber\" IS NOT NULL AND \"Status\" IN (0, 1, 2, 3, 6)");
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplicant>().WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionOffering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<EducationBoard>().WithMany().HasForeignKey(x => x.IntendedBoardId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<EducationMedium>().WithMany().HasForeignKey(x => x.IntendedMediumId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<EducationMediumStage>().WithMany().HasForeignKey(x => x.IntendedStageId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Standard>().WithMany().HasForeignKey(x => x.IntendedStandardId).OnDelete(DeleteBehavior.Restrict);
	}

	void IEntityTypeConfiguration<StudentOnboardingStep>.Configure(EntityTypeBuilder<StudentOnboardingStep> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentOnboardingSteps", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(StudentOnboardingStep.CompletedAt));
		builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
		builder.Property(x => x.Label).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Detail).HasMaxLength(1000);
		builder.Property(x => x.WaiverReason).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.OnboardingCaseId, x.Key }).IsUnique();
		builder.HasOne<StudentOnboardingCase>().WithMany().HasForeignKey(x => x.OnboardingCaseId).OnDelete(DeleteBehavior.Cascade);
	}

	void IEntityTypeConfiguration<StudentOnboardingEvent>.Configure(EntityTypeBuilder<StudentOnboardingEvent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentOnboardingEvents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.EventType).IsRequired().HasMaxLength(80);
		builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
		builder.Property(x => x.MetadataJson).HasColumnType("text");
		builder.HasIndex(x => new { x.TenantId, x.OnboardingCaseId, x.CreationTime });
		builder.HasOne<StudentOnboardingCase>().WithMany().HasForeignKey(x => x.OnboardingCaseId).OnDelete(DeleteBehavior.Cascade);
	}

	void IEntityTypeConfiguration<StudentOnboardingPolicy>.Configure(EntityTypeBuilder<StudentOnboardingPolicy> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentOnboardingPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique()
			.HasDatabaseName("IX_OnboardingPolicy_AcademicYearDefault")
			.HasFilter("\"IsDeleted\" = false AND \"OfferingId\" IS NULL AND \"StandardId\" IS NULL");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.OfferingId, x.StandardId }).IsUnique()
			.HasDatabaseName("IX_OnboardingPolicy_OfferingStandardOverride")
			.HasFilter("\"IsDeleted\" = false AND \"OfferingId\" IS NOT NULL AND \"StandardId\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive });
	}

	void IEntityTypeConfiguration<StudentOnboardingSettings>.Configure(EntityTypeBuilder<StudentOnboardingSettings> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentOnboardingSettings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SchemaVersion).IsRequired();
		builder.Property(x => x.ConfigurationJson).IsRequired().HasColumnType("text");
		builder.HasIndex(x => x.TenantId).IsUnique()
			.HasDatabaseName("IX_OnboardingSettings_SchoolDefault")
			.HasFilter("\"IsDeleted\" = false AND \"AcademicYearId\" IS NULL");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique()
			.HasDatabaseName("IX_OnboardingSettings_AcademicYear")
			.HasFilter("\"IsDeleted\" = false AND \"AcademicYearId\" IS NOT NULL");
		builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict);
	}

	void IEntityTypeConfiguration<StudentSeatReservation>.Configure(EntityTypeBuilder<StudentSeatReservation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentSeatReservations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(StudentSeatReservation.ReservedAt), nameof(StudentSeatReservation.ExpiresAt),
			nameof(StudentSeatReservation.CommittedAt), nameof(StudentSeatReservation.ReleasedAt));
		builder.Property(x => x.ReleaseReason).HasMaxLength(500);
		builder.HasIndex(x => new { x.TenantId, x.OnboardingCaseId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DivisionId, x.Status, x.ExpiresAt });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DivisionId, x.SeatNumber })
			.IsUnique()
			.HasFilter("\"Status\" = 0 AND \"IsDeleted\" = false");
		builder.HasOne<StudentOnboardingCase>().WithMany().HasForeignKey(x => x.OnboardingCaseId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Division>().WithMany().HasForeignKey(x => x.DivisionId).OnDelete(DeleteBehavior.Restrict);
	}

	void IEntityTypeConfiguration<StudentIdentifierSeries>.Configure(EntityTypeBuilder<StudentIdentifierSeries> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentIdentifierSeries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Prefix).IsRequired().HasMaxLength(40);
		builder.Property(x => x.Suffix).IsRequired().HasMaxLength(40);
		builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.StandardId, x.IdentifierType, x.IsActive });
	}

	void IEntityTypeConfiguration<StudentIdentifierAllocation>.Configure(EntityTypeBuilder<StudentIdentifierAllocation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentIdentifierAllocations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(StudentIdentifierAllocation.AllocatedAt), nameof(StudentIdentifierAllocation.CommittedAt));
		builder.Property(x => x.Value).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Reason).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.OnboardingCaseId, x.IdentifierType }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.IdentifierType, x.Value }).IsUnique();
		builder.HasOne<StudentOnboardingCase>().WithMany().HasForeignKey(x => x.OnboardingCaseId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<StudentIdentifierSeries>().WithMany().HasForeignKey(x => x.NumberSeriesId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}

	void IEntityTypeConfiguration<StudentOnboardingServiceHandoff>.Configure(EntityTypeBuilder<StudentOnboardingServiceHandoff> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentOnboardingServiceHandoffs", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(StudentOnboardingServiceHandoff.LastAttemptAt), nameof(StudentOnboardingServiceHandoff.AcknowledgedAt));
		builder.Property(x => x.ServiceKey).IsRequired().HasMaxLength(80);
		builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(160);
		builder.Property(x => x.LastError).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.OnboardingCaseId, x.ServiceKey }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.Status, x.LastAttemptAt });
		builder.HasOne<StudentOnboardingCase>().WithMany().HasForeignKey(x => x.OnboardingCaseId).OnDelete(DeleteBehavior.Restrict);
	}
}
