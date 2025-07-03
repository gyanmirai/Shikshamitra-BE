using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkAssignmentConfiguration : IEntityTypeConfiguration<HomeworkAssignmentEntity>
{
	public void Configure(EntityTypeBuilder<HomeworkAssignmentEntity> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "HomeworkAssignments",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();
	}
}
