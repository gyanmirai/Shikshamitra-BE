using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectTeacherConfiguration : IEntityTypeConfiguration<SubjectTeacher>
{
	public void Configure(EntityTypeBuilder<SubjectTeacher> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "SubjectTeachers", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		builder.HasKey(st => new { st.SubjectId, st.TeacherId });
	}
}
