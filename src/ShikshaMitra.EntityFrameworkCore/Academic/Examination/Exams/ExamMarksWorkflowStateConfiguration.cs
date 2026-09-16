using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Exams;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.Exams;

public class ExamMarksWorkflowStateConfiguration : IEntityTypeConfiguration<ExamMarksWorkflowState>
{
    public void Configure(EntityTypeBuilder<ExamMarksWorkflowState> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "ExamMarksWorkflowStates", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.SubjectId).IsRequired();
        builder.Property(x => x.DivisionId).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasDefaultValue(ExamMarksWorkflowStatus.Draft);

        builder.HasIndex(x => new { x.ExamId, x.SubjectId, x.DivisionId }).IsUnique();
    }
}
