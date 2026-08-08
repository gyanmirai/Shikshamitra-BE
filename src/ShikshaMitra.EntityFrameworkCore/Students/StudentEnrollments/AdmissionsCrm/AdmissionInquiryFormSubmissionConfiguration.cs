using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryFormSubmissionConfiguration : IEntityTypeConfiguration<AdmissionInquiryFormSubmission>
{
	public void Configure(EntityTypeBuilder<AdmissionInquiryFormSubmission> builder)
	{
		builder.ToTable("AppAdmissionInquiryFormSubmissions");
		builder.ConfigureByConvention();

		builder.Property(x => x.TemplateNameSnapshot)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength);
		builder.Property(x => x.SubmittedFieldsJson)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxSubmittedFieldsJsonLength);
		builder.Property(x => x.IpAddress)
			.HasMaxLength(AdmissionCrmConsts.MaxIpAddressLength);
		builder.Property(x => x.UserAgent)
			.HasMaxLength(AdmissionCrmConsts.MaxUserAgentLength);
		builder.Property(x => x.SubmissionKey)
			.HasMaxLength(AdmissionCrmConsts.MaxSubmissionKeyLength);

		builder.HasIndex(x => new { x.TenantId, x.LeadId });
		builder.HasIndex(x => new { x.TenantId, x.FormTemplateId });
		builder.HasIndex(x => new { x.TenantId, x.FormTemplateId, x.SubmissionKey })
			.IsUnique();
	}
}
