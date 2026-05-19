using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendancePolicyEvaluationRunConfiguration : IEntityTypeConfiguration<AttendancePolicyEvaluationRun>
{
	public void Configure(EntityTypeBuilder<AttendancePolicyEvaluationRun> builder)
	{
		builder.ToTable("HR_AttendancePolicyEvaluationRuns");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.ConfigHash).IsRequired().HasMaxLength(128);
		builder.Property(x => x.WarningsJson).HasColumnType("jsonb");
		builder.Property(x => x.EstimatedPayableDayDelta).HasPrecision(18, 2);

		builder.HasMany(x => x.Results)
			.WithOne(x => x.Run)
			.HasForeignKey(x => x.AttendancePolicyEvaluationRunId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Year, x.Month, x.Status })
			.HasDatabaseName("IX_AttendancePolicyEvaluationRuns_Month_Status");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Year, x.Month })
			.IsUnique()
			.HasFilter("\"Status\" = 4 AND \"TenantId\" IS NOT NULL")
			.HasDatabaseName("UX_AttendancePolicyEvaluationRuns_TenantPublishedMonth");

		builder.HasIndex(x => new { x.AcademicYearId, x.Year, x.Month })
			.IsUnique()
			.HasFilter("\"Status\" = 4 AND \"TenantId\" IS NULL")
			.HasDatabaseName("UX_AttendancePolicyEvaluationRuns_HostPublishedMonth");
	}
}
