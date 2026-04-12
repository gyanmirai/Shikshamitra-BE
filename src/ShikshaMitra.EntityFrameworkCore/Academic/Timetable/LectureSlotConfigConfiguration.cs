using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Timetable;

public class LectureSlotConfigConfiguration : IEntityTypeConfiguration<LectureSlotConfig>
{
    public void Configure(EntityTypeBuilder<LectureSlotConfig> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "LectureSlotConfigs",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasIndex(x => x.StandardId).IsUnique();

        builder.Property(x => x.SchoolHours)
            .IsRequired()
            .HasMaxLength(TimetableConsts.MaxSchoolHoursLength);

        builder.HasMany(x => x.SlotGroups)
            .WithOne()
            .HasForeignKey(x => x.LectureSlotConfigId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LectureSlotGroupConfiguration : IEntityTypeConfiguration<LectureSlotGroup>
{
    public void Configure(EntityTypeBuilder<LectureSlotGroup> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "LectureSlotGroups",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasMany(x => x.TimeSlots)
            .WithOne()
            .HasForeignKey(x => x.LectureSlotGroupId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LectureSlotTimeSlotConfiguration : IEntityTypeConfiguration<LectureSlotTimeSlot>
{
    public void Configure(EntityTypeBuilder<LectureSlotTimeSlot> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "LectureSlotTimeSlots",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();
    }
}
