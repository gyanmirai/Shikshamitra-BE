using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Syllabus;

public class SubjectChapterConfiguration : IEntityTypeConfiguration<AssignedSubjectChapter>
{
	public void Configure(EntityTypeBuilder<AssignedSubjectChapter> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "SubjectChapters", ShikshaMitraConsts.DbSchema);

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

		builder.Property(x => x.UnitName).HasMaxLength(128);
		builder.Property(x => x.TermName).HasMaxLength(128);
		builder.Property(x => x.Topic).HasMaxLength(256);
		builder.Property(x => x.DifficultyLevel).HasMaxLength(64);
		builder.Property(x => x.BloomLevel).HasMaxLength(64);
		builder.Property(x => x.Visibility).HasMaxLength(64);
		builder.Property(x => x.PublishStatus).HasMaxLength(64);
		builder.Property(x => x.ReviewStatus).HasMaxLength(64);
		builder.Property(x => x.CoverageStatus).HasMaxLength(64);
		builder.Property(x => x.LessonPlanStatus).HasMaxLength(64);
		builder.Property(x => x.HomeworkStatus).HasMaxLength(64);
		builder.Property(x => x.AssessmentStatus).HasMaxLength(64);
		builder.Property(x => x.ResourceStatus).HasMaxLength(64);
		builder.Property(x => x.TemplateSource).HasMaxLength(128);
		builder.Property(x => x.VersionLabel).HasMaxLength(64);
	}
}
