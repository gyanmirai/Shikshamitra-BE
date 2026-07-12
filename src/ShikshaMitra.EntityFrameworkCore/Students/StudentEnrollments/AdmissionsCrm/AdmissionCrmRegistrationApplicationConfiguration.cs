using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationApplicationConfiguration : IEntityTypeConfiguration<AdmissionCrmRegistrationApplication>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmRegistrationApplication> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmRegistrationApplications", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.FormNumber).HasMaxLength(AdmissionCrmConsts.MaxRegistrationFormNumberLength).IsRequired();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.TemplateSnapshotJson).HasMaxLength(AdmissionCrmConsts.MaxRegistrationTemplateJsonLength).IsRequired();
		builder.Property(x => x.PrefillJson).HasMaxLength(AdmissionCrmConsts.MaxRegistrationPrefillJsonLength).IsRequired();
		builder.Property(x => x.ResponseJson).HasMaxLength(AdmissionCrmConsts.MaxRegistrationResponseJsonLength).IsRequired();
		builder.Property(x => x.CorrectionJson).HasMaxLength(AdmissionCrmConsts.MaxRegistrationCorrectionJsonLength).IsRequired();
		builder.Property(x => x.CompletionPercentage).HasPrecision(5, 2);
		builder.Property(x => x.FeeAmount).HasPrecision(18, 2);
		builder.Property(x => x.PaymentReference).HasMaxLength(AdmissionCrmConsts.MaxPaymentReferenceLength);
		builder.Property(x => x.LifecycleReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.FormNumber })
			.IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.LinkExpiresAt, x.Status });
	}
}
