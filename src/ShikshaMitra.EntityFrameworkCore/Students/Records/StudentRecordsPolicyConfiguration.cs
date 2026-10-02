using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Records;

public class StudentRecordsPolicyConfiguration : IEntityTypeConfiguration<StudentRecordsConfiguration>, IEntityTypeConfiguration<StudentRecordsPolicyVersion>
{
	public void Configure(EntityTypeBuilder<StudentRecordsConfiguration> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordsConfigurations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.DraftPolicyJson).IsRequired().HasColumnType("text");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId }).IsUnique().HasFilter("\"AcademicYearId\" IS NOT NULL AND \"IsDeleted\" = false");
		builder.HasIndex(x => x.TenantId).IsUnique().HasFilter("\"AcademicYearId\" IS NULL AND \"IsDeleted\" = false");
	}
	public void Configure(EntityTypeBuilder<StudentRecordsPolicyVersion> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordsPolicyVersions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.PolicyJson).IsRequired().HasColumnType("text");
		builder.HasIndex(x => new { x.TenantId, x.ConfigurationId, x.VersionNumber }).IsUnique();
		builder.HasOne<StudentRecordsConfiguration>().WithMany().HasForeignKey(x => x.ConfigurationId).OnDelete(DeleteBehavior.Restrict);
	}
}
