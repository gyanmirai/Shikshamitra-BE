using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryFormTemplateConfiguration : IEntityTypeConfiguration<AdmissionInquiryFormTemplate>
{
	public void Configure(EntityTypeBuilder<AdmissionInquiryFormTemplate> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionInquiryFormTemplates",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.TemplateName)
			.HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength)
			.IsRequired();
		builder.Property(x => x.Description)
			.HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.Property(x => x.Channel)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.ConsentText)
			.HasMaxLength(AdmissionCrmConsts.MaxConsentTextLength);
		builder.Property(x => x.PrivacyNoticeUrl)
			.HasMaxLength(AdmissionCrmConsts.MaxUrlLength);
		builder.Property(x => x.FieldsJson)
			.HasMaxLength(AdmissionCrmConsts.MaxFieldsJsonLength)
			.IsRequired();
		builder.Property(x => x.LocalizationsJson).HasColumnType("jsonb").IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.TemplateName })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Channel });
	}
}
