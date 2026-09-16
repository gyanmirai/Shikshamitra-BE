using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Timetable.Timetables;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableConfiguration : IEntityTypeConfiguration<Timetables.Timetable>
{
    public void Configure(EntityTypeBuilder<Timetables.Timetable> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.AcademicDbTablePrefix + "Timetables",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasIndex(x => new { x.StandardId, x.DivisionId }).IsUnique();

        builder.HasMany(x => x.Slots)
            .WithOne()
            .HasForeignKey(x => x.TimetableId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TimetableSlotConfiguration : IEntityTypeConfiguration<TimetableSlot>
{
    public void Configure(EntityTypeBuilder<TimetableSlot> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.AcademicDbTablePrefix + "TimetableSlots",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasMany(x => x.Subjects)
            .WithOne()
            .HasForeignKey(x => x.TimetableSlotId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TimetableSlotSubjectConfiguration : IEntityTypeConfiguration<TimetableSlotSubject>
{
    public void Configure(EntityTypeBuilder<TimetableSlotSubject> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.AcademicDbTablePrefix + "TimetableSlotSubjects",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();
    }
}
