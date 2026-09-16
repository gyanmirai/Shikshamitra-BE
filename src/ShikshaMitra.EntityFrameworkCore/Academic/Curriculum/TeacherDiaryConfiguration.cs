using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class TeacherDiaryConfiguration : IEntityTypeConfiguration<TeacherDiary>
{
    public void Configure(EntityTypeBuilder<TeacherDiary> builder)
    {
        builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "TeacherDiaries", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ActualTopicCovered).HasMaxLength(256);
        builder.Property(x => x.ActualContentSummary).HasMaxLength(2048);
        builder.Property(x => x.TeachingMethodUsed).HasMaxLength(256);
        builder.Property(x => x.StudentResponse).HasMaxLength(1024);
        builder.Property(x => x.ClassUnderstandingLevel).HasMaxLength(64);
        builder.Property(x => x.StudentEngagementLevel).HasMaxLength(64);
        builder.Property(x => x.Reflection).HasMaxLength(2048);
        builder.Property(x => x.WhatWentWell).HasMaxLength(1024);
        builder.Property(x => x.WhatWasDifficult).HasMaxLength(1024);
        builder.Property(x => x.StudentsNeedingSupport).HasMaxLength(1024);
        builder.Property(x => x.NextClassFollowUp).HasMaxLength(1024);
        builder.Property(x => x.RemedialActionRequired).HasMaxLength(1024);
        builder.Property(x => x.HomeworkAssigned).HasMaxLength(1024);
        builder.Property(x => x.MissedReason).HasMaxLength(128);
        builder.Property(x => x.BackdatedReason).HasMaxLength(1024);
        builder.Property(x => x.TeacherRemarks).HasMaxLength(2048);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();

        builder.HasIndex(x => x.LessonPlanId).IsUnique();
        builder.HasIndex(x => new { x.TeacherId, x.ActualDate });
    }
}
