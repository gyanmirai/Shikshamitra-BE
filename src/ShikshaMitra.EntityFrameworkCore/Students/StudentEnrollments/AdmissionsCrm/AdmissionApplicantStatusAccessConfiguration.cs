using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionApplicantStatusAccessConfiguration : IEntityTypeConfiguration<AdmissionApplicantStatusAccess>
{
	public void Configure(EntityTypeBuilder<AdmissionApplicantStatusAccess> builder)
	{
		builder.ToTable("AppAdmissionApplicantStatusAccesses");
		builder.ConfigureByConvention();

		builder.Property(x => x.TokenHash)
			.IsRequired()
			.HasMaxLength(AdmissionCrmConsts.MaxTokenHashLength);
		builder.Property(x => x.EncryptedToken)
			.HasMaxLength(AdmissionCrmConsts.MaxEncryptedTokenLength);
		builder.Property(x => x.OtpCodeHash)
			.HasMaxLength(AdmissionCrmConsts.MaxOtpHashLength);
		builder.Property(x => x.EncryptedOtpCode)
			.HasMaxLength(AdmissionCrmConsts.MaxEncryptedOtpLength);
		builder.Property(x => x.AccessSessionTokenHash)
			.HasMaxLength(AdmissionCrmConsts.MaxTokenHashLength);

		builder.HasIndex(x => new { x.TenantId, x.TokenHash })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.LeadId });
		builder.HasIndex(x => new { x.TenantId, x.OtpExpiresAt });
		builder.HasIndex(x => new { x.TenantId, x.AccessSessionExpiresAt });
		builder.HasIndex(x => new { x.TenantId, x.FamilyAccessGuardianPersonId });
	}
}
