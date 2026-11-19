using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRecordCustomProfileConfiguration : IEntityTypeConfiguration<StudentRecordCustomProfile>
{
	public void Configure(EntityTypeBuilder<StudentRecordCustomProfile> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordCustomProfiles", ShikshaMitraConsts.DbSchema);
		b.ConfigureByConvention();
		b.Property(x => x.SourcesJson).IsRequired();
		b.Property(x => x.SchemaJson).IsRequired();
		b.Property(x => x.ValuesJson).IsRequired();
		b.HasIndex(x => new { x.TenantId, x.StudentId }).IsUnique().HasFilter("\"IsDeleted\" = false");
		b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
}
