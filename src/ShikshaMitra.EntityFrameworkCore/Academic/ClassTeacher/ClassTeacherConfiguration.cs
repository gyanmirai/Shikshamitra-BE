using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.ClassTeacher;

public class ClassTeacherConfiguration : IEntityTypeConfiguration<ClassTeacher>
{
	public void Configure(EntityTypeBuilder<ClassTeacher> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "ClassTeachers", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
