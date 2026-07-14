using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmApplicantChecklistProgressConfiguration :
	IEntityTypeConfiguration<AdmissionCrmApplicantChecklistProgress>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmApplicantChecklistProgress> builder)
	{
		builder.ToTable("AppAdmissionCrmApplicantChecklistProgresses");
		builder.ConfigureByConvention();

		builder.Property(x => x.ChecklistKey)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxPurposeLength);
		builder.Property(x => x.Status)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Notes)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);

		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.ChecklistKey })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}
