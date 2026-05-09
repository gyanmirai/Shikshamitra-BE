using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.Exams;

public class ExamMarkConfiguration : IEntityTypeConfiguration<ExamMark>
{
	public void Configure(EntityTypeBuilder<ExamMark> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamMarks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.ExamId).IsRequired();
		builder.Property(x => x.StudentId).IsRequired();
		builder.Property(x => x.SubjectId).IsRequired();
		builder.Property(x => x.DivisionId).IsRequired();
		builder.Property(x => x.MarksObtained).IsRequired(false);
		builder.Property(x => x.IsAbsent).IsRequired();
		builder.Property(x => x.Feedback).HasMaxLength(1000).IsRequired(false);
		builder.Property(x => x.IsPublished).IsRequired().HasDefaultValue(false);

		builder.HasIndex(x => new { x.ExamId, x.StudentId, x.SubjectId }).IsUnique();
	}
}
