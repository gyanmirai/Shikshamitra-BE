using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class StudentSubmissionConfiguration : IEntityTypeConfiguration<StudentSubmissionEntity>
{
	public void Configure(EntityTypeBuilder<StudentSubmissionEntity> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "StudentHomeworkAssignmentSubmissions",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();
	}
}
