using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionIntakeSetupConfiguration : IEntityTypeConfiguration<AdmissionIntakeSetup>
{
	public void Configure(EntityTypeBuilder<AdmissionIntakeSetup> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionIntakeSetups", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.Name).IsRequired().HasMaxLength(128);
		builder.Property(item => item.IsArchived).IsRequired().HasDefaultValue(false);
		builder.HasIndex(item => new { item.TenantId, item.IsArchived, item.Name });
		builder.HasIndex(item => new { item.TenantId, item.InquiryIntakeProfileId }).IsUnique()
			.HasDatabaseName("UX_AdmissionIntakeSetups_TenantInquiryProfile")
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(item => new { item.TenantId, item.CounsellorAssignmentProfileId }).IsUnique()
			.HasDatabaseName("UX_AdmissionIntakeSetups_TenantAssignmentProfile")
			.HasFilter("\"IsDeleted\" = false");
		builder.HasOne<AdmissionIntakeProfile>().WithMany()
			.HasForeignKey(item => item.InquiryIntakeProfileId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionIntakeProfile>().WithMany()
			.HasForeignKey(item => item.CounsellorAssignmentProfileId).OnDelete(DeleteBehavior.Restrict);
	}
}
