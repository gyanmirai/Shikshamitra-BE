using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
	public void Configure(EntityTypeBuilder<StudentGroup> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentGroups", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
