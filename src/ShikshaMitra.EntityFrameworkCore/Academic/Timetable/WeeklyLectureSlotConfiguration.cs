using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Timetable;

public class WeeklyLectureSlotConfiguration : IEntityTypeConfiguration<WeeklyLectureSlot>
{
    public void Configure(EntityTypeBuilder<WeeklyLectureSlot> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.AcademicDbTablePrefix + "WeeklyLectureSlots",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasIndex(x => x.StandardId).IsUnique();

        builder.HasMany(x => x.Subjects)
            .WithOne()
            .HasForeignKey(x => x.WeeklyLectureSlotId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WeeklyLectureSlotSubjectConfiguration : IEntityTypeConfiguration<WeeklyLectureSlotSubject>
{
    public void Configure(EntityTypeBuilder<WeeklyLectureSlotSubject> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.AcademicDbTablePrefix + "WeeklyLectureSlotSubjects",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();
    }
}
