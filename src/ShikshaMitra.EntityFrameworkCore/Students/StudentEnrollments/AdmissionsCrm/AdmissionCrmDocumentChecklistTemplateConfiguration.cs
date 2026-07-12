using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentChecklistTemplateConfiguration : IEntityTypeConfiguration<AdmissionCrmDocumentChecklistTemplate>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmDocumentChecklistTemplate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmDocumentChecklistTemplates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength).IsRequired();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.ApplicabilityJson).HasMaxLength(AdmissionCrmConsts.MaxDocumentChecklistJsonLength).IsRequired();
		builder.Property(x => x.RequirementsJson).HasMaxLength(AdmissionCrmConsts.MaxDocumentChecklistJsonLength).IsRequired();
		builder.Property(x => x.ReadinessApprovalCount).IsRequired();
		builder.Property(x => x.Notes).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status })
			.HasDatabaseName("IX_AdmissionCrmDocTemplate_TenantYearStatus");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name, x.Version })
			.IsUnique()
			.HasDatabaseName("UX_AdmissionCrmDocTemplate_TenantYearNameVersion")
			.HasFilter("\"IsDeleted\" = false");
	}
}
