using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAutomationRunConfiguration : IEntityTypeConfiguration<AdmissionCrmAutomationRun>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmAutomationRun> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmAutomationRuns",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.RunType)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Status)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Trigger)
			.HasConversion<int>();
		builder.Property(x => x.Stage)
			.HasConversion<int>();
		builder.Property(x => x.Details)
			.HasMaxLength(AdmissionCrmConsts.MaxTimelineDetailsLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.CompletedAt });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.RunType, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.LeadId });
	}
}
