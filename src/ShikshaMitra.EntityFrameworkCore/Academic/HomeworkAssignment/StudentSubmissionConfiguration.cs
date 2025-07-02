using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public class StudentSubmissionConfiguration : IEntityTypeConfiguration<StudentSubmissionEntity>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<StudentSubmissionEntity> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "StudentHomeworkAssignmentSubmissions",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();
	}
}
