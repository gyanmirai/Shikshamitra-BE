using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
	public void Configure(EntityTypeBuilder<Subject> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Subjects", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
