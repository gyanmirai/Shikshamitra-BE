using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Syllabus;

public class SubjectChapterConfiguration : IEntityTypeConfiguration<AssignedSubjectChapter>
{
	public void Configure(EntityTypeBuilder<AssignedSubjectChapter> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "SubjectChapters", ShikshaMitraConsts.DbSchema);
		
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.ChapterName)
			.IsRequired()
			.HasMaxLength(ChapterConsts.MaxChapterNameLength);

		builder.Property(x => x.Description)
			.HasMaxLength(ChapterConsts.MaxDescriptionLength)
			.IsRequired(false);

		builder.Property(x => x.ChapterNumber)
			.IsRequired();
	}
}
