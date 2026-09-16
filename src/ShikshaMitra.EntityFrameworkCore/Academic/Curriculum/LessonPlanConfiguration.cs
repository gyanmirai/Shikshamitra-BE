using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanConfiguration : IEntityTypeConfiguration<LessonPlan>
{
    public void Configure(EntityTypeBuilder<LessonPlan> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "LessonPlans", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Topic)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.LearningObjective)
            .HasMaxLength(1024);

        builder.Property(x => x.TeachingMethod)
            .HasMaxLength(256);

        builder.Property(x => x.ResourcesRequired)
            .HasMaxLength(512);

        builder.Property(x => x.ReviewRemarks)
            .HasMaxLength(1024);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(x => x.TeacherDiary)
            .WithOne()
            .HasForeignKey<TeacherDiary>(x => x.LessonPlanId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasIndex(x => new { x.TeacherId, x.PlannedDate });
        builder.HasIndex(x => x.TimetableSlotId);
        // Supports the measured academic-year list query without sorting the whole year's plans.
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.CreationTime })
            .IsDescending(false, false, true)
            .HasFilter("NOT \"IsDeleted\"");
    }
}
