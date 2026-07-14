using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Timetable;

public class RoomReservationConfiguration : IEntityTypeConfiguration<RoomReservation>
{
    public void Configure(EntityTypeBuilder<RoomReservation> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "RoomReservations",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasIndex(x => new { x.RoomId, x.Date });
        builder.HasIndex(x => x.RecurringConfigId);
        
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
    }
}

public class RoomRecurringConfigConfiguration : IEntityTypeConfiguration<RoomRecurringConfig>
{
    public void Configure(EntityTypeBuilder<RoomRecurringConfig> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "RoomRecurringConfigs",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();
    }
}

public class RoomMaintenanceBlockConfiguration : IEntityTypeConfiguration<RoomMaintenanceBlock>
{
    public void Configure(EntityTypeBuilder<RoomMaintenanceBlock> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "RoomMaintenanceBlocks",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasIndex(x => new { x.RoomId, x.StartDateTime, x.EndDateTime });
        
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
    }
}

public class RoomConfigConfiguration : IEntityTypeConfiguration<RoomConfig>
{
    public void Configure(EntityTypeBuilder<RoomConfig> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "RoomConfigs",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();
    }
}

public class RoomAuditLogConfiguration : IEntityTypeConfiguration<RoomAuditLog>
{
    public void Configure(EntityTypeBuilder<RoomAuditLog> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "RoomAuditLogs",
            ShikshaMitraConsts.DbSchema);

        builder.ConfigureByConvention();

        builder.HasIndex(x => x.RoomId);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
        builder.Property(x => x.UserName).IsRequired().HasMaxLength(256);
    }
}
