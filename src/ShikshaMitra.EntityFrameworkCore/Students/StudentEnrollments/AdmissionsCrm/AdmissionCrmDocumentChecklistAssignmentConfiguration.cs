using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentChecklistAssignmentConfiguration : IEntityTypeConfiguration<AdmissionCrmDocumentChecklistAssignment>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmDocumentChecklistAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmDocumentChecklistAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TemplateSnapshotJson).HasMaxLength(AdmissionCrmConsts.MaxDocumentChecklistJsonLength).IsRequired();
		builder.Property(x => x.ReadinessReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.ReadinessApprovalCount).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.RegistrationApplicationId }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.LeadId });
	}
}
