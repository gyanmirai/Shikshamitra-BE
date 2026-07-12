using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionInquiryConsentRecordConfiguration : IEntityTypeConfiguration<AdmissionInquiryConsentRecord>
{
	public void Configure(EntityTypeBuilder<AdmissionInquiryConsentRecord> builder)
	{
		builder.ToTable("AppAdmissionInquiryConsentRecords");
		builder.ConfigureByConvention();

		builder.Property(x => x.TemplateNameSnapshot)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength);
		builder.Property(x => x.ConsentTextSnapshot)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxConsentTextLength);
		builder.Property(x => x.PrivacyNoticeUrl)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxUrlLength);
		builder.Property(x => x.ConsentVersion)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxConsentVersionLength);
		builder.Property(x => x.CommunicationPreferencesJson)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxCommunicationPreferencesJsonLength);
		builder.Property(x => x.IpAddress)
			.HasMaxLength(AdmissionCrmConsts.MaxIpAddressLength);
		builder.Property(x => x.UserAgent)
			.HasMaxLength(AdmissionCrmConsts.MaxUserAgentLength);

		builder.HasIndex(x => new { x.TenantId, x.LeadId });
		builder.HasIndex(x => new { x.TenantId, x.FormTemplateId });
	}
}
