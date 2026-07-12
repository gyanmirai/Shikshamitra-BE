using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationTemplateConfiguration : IEntityTypeConfiguration<AdmissionCrmRegistrationTemplate>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmRegistrationTemplate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmRegistrationTemplates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength).IsRequired();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.ApplicabilityJson).HasMaxLength(AdmissionCrmConsts.MaxRegistrationTemplateJsonLength).IsRequired();
		builder.Property(x => x.SectionsJson).HasMaxLength(AdmissionCrmConsts.MaxRegistrationTemplateJsonLength).IsRequired();
		builder.Property(x => x.FeeAmount).HasPrecision(18, 2);
		builder.Property(x => x.AutoIssueAtStage);
		builder.Property(x => x.Notes).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status })
			.HasDatabaseName("IX_AdmissionCrmRegTemplate_TenantYearStatus");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name, x.Version })
			.IsUnique()
			.HasDatabaseName("UX_AdmissionCrmRegTemplate_TenantYearNameVersion")
			.HasFilter("\"IsDeleted\" = false");
	}
}
