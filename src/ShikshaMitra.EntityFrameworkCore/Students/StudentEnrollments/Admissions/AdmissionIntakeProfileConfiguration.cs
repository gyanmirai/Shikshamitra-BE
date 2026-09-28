using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionIntakeProfileConfiguration : IEntityTypeConfiguration<AdmissionIntakeProfile>
{
	public void Configure(EntityTypeBuilder<AdmissionIntakeProfile> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionIntakeProfiles", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.Kind).IsRequired();
		builder.Property(item => item.Code).IsRequired().HasMaxLength(64);
		builder.Property(item => item.Name).IsRequired().HasMaxLength(128);
		builder.Property(item => item.ConfigurationJson).IsRequired().HasColumnType("text");
		builder.HasIndex(item => new { item.TenantId, item.Kind, item.Code }).IsUnique()
			.HasDatabaseName("UX_AdmissionIntakeProfiles_TenantKindCode")
			.HasFilter("\"IsDeleted\" = false");
	}
}
