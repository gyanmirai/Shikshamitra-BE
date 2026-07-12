using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmMessageTemplateConfiguration : IEntityTypeConfiguration<AdmissionCrmMessageTemplate>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmMessageTemplate> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmMessageTemplates",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.TemplateName)
			.HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength)
			.IsRequired();
		builder.Property(x => x.Purpose)
			.HasMaxLength(AdmissionCrmConsts.MaxPurposeLength)
			.IsRequired();
		builder.Property(x => x.Subject)
			.HasMaxLength(AdmissionCrmConsts.MaxMessageSubjectLength);
		builder.Property(x => x.Body)
			.HasMaxLength(AdmissionCrmConsts.MaxMessageBodyLength)
			.IsRequired();
		builder.Property(x => x.VariablesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxTemplateVariablesJsonLength)
			.IsRequired();
		builder.Property(x => x.Channel)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Status)
			.HasConversion<int>()
			.IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Channel, x.TemplateName })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive });
	}
}
