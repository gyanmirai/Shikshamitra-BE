using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFollowUpTaskConfiguration : IEntityTypeConfiguration<AdmissionCrmFollowUpTask>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmFollowUpTask> builder)
	{
		builder.ToTable("AppAdmissionCrmFollowUpTasks");
		builder.ConfigureByConvention();

		builder.Property(x => x.Title)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.Property(x => x.Status)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Notes)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.CompletionNotes)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);

		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DueAt });
		builder.HasIndex(x => new { x.TenantId, x.AssignedUserId, x.Status });
	}
}
