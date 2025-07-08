using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Homework;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkAssignmentTypeConfiguration : IEntityTypeConfiguration<HomeworkAssignmentType>
{
	public void Configure(EntityTypeBuilder<HomeworkAssignmentType> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "HomeworkAssignmentTypes",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();
	}
}
