using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarConfiguration : IEntityTypeConfiguration<AcademicCalendar>
{
    public void Configure(EntityTypeBuilder<AcademicCalendar> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicCalendars", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(1024);
        
        builder.HasMany(x => x.Days)
               .WithOne()
               .HasForeignKey(x => x.AcademicCalendarId)
               .OnDelete(DeleteBehavior.Cascade);
               
        builder.HasIndex(x => new { x.AcademicYearId, x.IsDefault });
    }
}

public class AcademicCalendarDayConfiguration : IEntityTypeConfiguration<AcademicCalendarDay>
{
    public void Configure(EntityTypeBuilder<AcademicCalendarDay> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicCalendarDays", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        
        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(1024);
        builder.Property(x => x.ChangeReason).HasMaxLength(1024);
        builder.Property(x => x.AutoScanImpacts).IsRequired().HasDefaultValue(true);
        
        // Non-unique: a multi-class marker stores one row per class on the same date;
        // duplicate protection (per class / applies-to-all) is enforced in the app service.
        builder.HasIndex(x => new { x.AcademicCalendarId, x.Date });
        builder.HasIndex(x => new { x.Date, x.DayType });
    }
}

public class AcademicCalendarWorkingHourConfiguration : IEntityTypeConfiguration<AcademicCalendarWorkingHour>
{
    public void Configure(EntityTypeBuilder<AcademicCalendarWorkingHour> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicCalendarWorkingHours", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        
        builder.HasIndex(x => new { x.AcademicCalendarId, x.StandardId, x.DivisionId });
    }
}

public class AcademicCalendarSettingConfiguration : IEntityTypeConfiguration<AcademicCalendarSetting>
{
    public void Configure(EntityTypeBuilder<AcademicCalendarSetting> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicCalendarSettings", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        
        builder.HasIndex(x => x.AcademicYearId).IsUnique();
    }
}

public class CalendarImpactLogConfiguration : IEntityTypeConfiguration<CalendarImpactLog>
{
    public void Configure(EntityTypeBuilder<CalendarImpactLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "CalendarImpactLogs", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        
        builder.Property(x => x.AffectedEntityName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.ResolutionNote).HasMaxLength(1024);
        
        builder.HasIndex(x => new { x.AcademicCalendarDayId, x.Status });
    }
}

public class AcademicCalendarAuditLogConfiguration : IEntityTypeConfiguration<AcademicCalendarAuditLog>
{
    public void Configure(EntityTypeBuilder<AcademicCalendarAuditLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicCalendarAuditLogs", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.OldValueJson).IsRequired();
        builder.Property(x => x.NewValueJson).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1024);

        builder.Property(x => x.Module).IsRequired().HasMaxLength(64).HasDefaultValue("CalendarManagement");
        builder.Property(x => x.SubModule).HasMaxLength(64);
        builder.Property(x => x.EntityType).HasMaxLength(64);
        builder.Property(x => x.EntityName).HasMaxLength(256);
        builder.Property(x => x.ChangedFieldsJson).HasColumnType("text");
        builder.Property(x => x.ActorNameSnapshot).HasMaxLength(128);
        builder.Property(x => x.ActorRoleSnapshot).HasMaxLength(64);
        builder.Property(x => x.ActorEmailSnapshot).HasMaxLength(256);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.Device).HasMaxLength(64);
        builder.Property(x => x.Browser).HasMaxLength(64);
        builder.Property(x => x.OperatingSystem).HasMaxLength(64);
        builder.Property(x => x.RequestPath).HasMaxLength(512);
        builder.Property(x => x.HttpMethod).HasMaxLength(16);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);

        builder.HasIndex(x => x.AcademicCalendarId);
        builder.HasIndex(x => new { x.TenantId, x.Module, x.ChangedAt })
            .HasDatabaseName("IX_AppAcademicCalendarAuditLogs_Tenant_Module_ChangedAt");
    }
}
