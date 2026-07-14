using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamRecordConfiguration : IEntityTypeConfiguration<AdmissionCrmEntranceExamRecord>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmEntranceExamRecord> builder)
	{
		builder.ToTable("AppAdmissionCrmEntranceExamRecords");
		builder.ConfigureByConvention();

		builder.Property(x => x.SlotNameSnapshot)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.CapacityOverrideReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.AttendanceStatus)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.ExamStatus)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.EligibilityDecision)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.EligibilityDecisionReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.EligibilityOverrideReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.ExamStartTime });
		builder.HasIndex(x => new { x.TenantId, x.LeadId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.EntranceExamSlotId });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.EligibilityDecision });
	}
}
