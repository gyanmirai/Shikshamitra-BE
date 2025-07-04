using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Syllabus;

public class SubjectChapterConfiguration : IEntityTypeConfiguration<SubjectChapter>
{
	public void Configure(EntityTypeBuilder<SubjectChapter> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "SubjectChapters", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
