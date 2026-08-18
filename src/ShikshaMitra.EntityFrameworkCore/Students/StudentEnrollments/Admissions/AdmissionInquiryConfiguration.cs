using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryConfiguration : IEntityTypeConfiguration<AdmissionInquiry>
{
	public void Configure(EntityTypeBuilder<AdmissionInquiry> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionInquiries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.ApplicantName).IsRequired().HasMaxLength(AdmissionInquiryConsts.MaxNameLength);
		builder.Property(x => x.InquiryNumber).IsRequired().HasMaxLength(AdmissionInquiryConsts.MaxInquiryNumberLength);
		builder.HasIndex(x => new { x.TenantId, x.InquiryNumber }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.FamilyId });
		builder.HasIndex(x => new { x.TenantId, x.ApplicantId });
		builder.Property(x => x.NormalizedPhone).IsRequired().HasMaxLength(AdmissionInquiryConsts.MaxPhoneLength);
		builder.Property(x => x.Email).HasMaxLength(AdmissionInquiryConsts.MaxEmailLength);
		builder.Property(x => x.SourceDetail).HasMaxLength(AdmissionInquiryConsts.MaxSourceDetailLength);
		builder.HasIndex(x => new { x.TenantId, x.AdmissionCycleId, x.NormalizedPhone });
		builder.HasIndex(x => new { x.TenantId, x.AdmissionCycleId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.AssignedCounsellorId });
		builder.HasOne<AdmissionOffering>().WithMany().HasForeignKey(x => x.AdmissionOfferingId).OnDelete(DeleteBehavior.Restrict);
	}
}
