using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Examination.SeatAllocation;

public class ExamSeatingPlanSubjectConfiguration : IEntityTypeConfiguration<ExamSeatingPlanSubject>
{
    public void Configure(EntityTypeBuilder<ExamSeatingPlanSubject> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ExamSeatingPlanSubjects", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.SeatingPlanId).IsRequired();
        builder.Property(x => x.ExamId).IsRequired();
        builder.Property(x => x.ExamSubjectId).IsRequired();
        builder.Property(x => x.SubjectId).IsRequired();

        builder.HasIndex(x => new { x.SeatingPlanId, x.ExamSubjectId }).IsUnique();
        builder.HasIndex(x => new { x.ExamId, x.ExamSubjectId });
    }
}
