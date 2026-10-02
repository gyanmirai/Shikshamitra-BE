using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRecordLegacyOverrideConfiguration : IEntityTypeConfiguration<StudentRecordLegacyOverride>
{
	public void Configure(EntityTypeBuilder<StudentRecordLegacyOverride> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordLegacyOverrides", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
		builder.Property(x => x.EvidenceReference).HasMaxLength(1000);
		builder.Property(x => x.DecisionRemarks).HasMaxLength(2000);
		builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.Status }).IsUnique().HasFilter("\"Status\" IN ('Pending', 'Approved') AND \"IsDeleted\" = false");
	}
}
