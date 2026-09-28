using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionPublicInquiryLinkConfiguration : IEntityTypeConfiguration<AdmissionPublicInquiryLink>
{
	public void Configure(EntityTypeBuilder<AdmissionPublicInquiryLink> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionPublicInquiryLinks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.Name).IsRequired().HasMaxLength(128);
		builder.Property(item => item.TokenHash).IsRequired().HasMaxLength(64);
		builder.Property(item => item.SourceDetail).HasMaxLength(512);
		builder.HasIndex(item => item.InquiryFormDefinitionId);
		builder.HasOne<AdmissionFormDefinition>().WithMany()
			.HasForeignKey(item => item.InquiryFormDefinitionId)
			.OnDelete(DeleteBehavior.Restrict)
			.HasConstraintName("FK_PublicInquiryLinks_InquiryForm");
		builder.HasIndex(item => item.TokenHash).IsUnique();
		builder.HasIndex(item => new { item.TenantId, item.AdmissionCycleId, item.IsActive });
	}
}
