using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Timetable.Substitutions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Timetable;

public class SubstitutionRequestConfiguration : IEntityTypeConfiguration<SubstitutionRequest>
{
    public void Configure(EntityTypeBuilder<SubstitutionRequest> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicSubstitutionRequests", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => new { x.AcademicYearId, x.Date, x.Status });
        builder.HasIndex(x => new { x.Date, x.TimetableSlotId, x.TimetableSlotSubjectId, x.OriginalTeacherId });
        builder.HasIndex(x => new { x.SubstituteTeacherId, x.Date, x.StartTime, x.EndTime });
        builder.Property(x => x.Day).HasMaxLength(32);
        builder.Property(x => x.Period).HasMaxLength(64);
        builder.Property(x => x.ClassName).HasMaxLength(128);
        builder.Property(x => x.DivisionName).HasMaxLength(128);
        builder.Property(x => x.SubjectName).HasMaxLength(256);
        builder.Property(x => x.DepartmentName).HasMaxLength(256);
        builder.Property(x => x.OriginalTeacherName).HasMaxLength(256);
        builder.Property(x => x.SubstituteTeacherName).HasMaxLength(256);
        builder.Property(x => x.RoomName).HasMaxLength(256);
        builder.Property(x => x.AssignedRoomName).HasMaxLength(256);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.NotificationStatus).HasMaxLength(64);
        builder.Property(x => x.ConflictReason).HasMaxLength(1000);
        builder.Property(x => x.WorkloadStatus).HasMaxLength(64);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
    }
}

public class TeacherAvailabilityConfiguration : IEntityTypeConfiguration<TeacherAvailability>
{
    public void Configure(EntityTypeBuilder<TeacherAvailability> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicTeacherAvailabilities", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => new { x.AcademicYearId, x.TeacherId, x.AvailabilityDate });
        builder.HasIndex(x => new { x.TeacherName, x.AvailabilityDate });
        builder.Property(x => x.TeacherName).HasMaxLength(256);
        builder.Property(x => x.DepartmentName).HasMaxLength(256);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.AttachmentName).HasMaxLength(512);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
    }
}

public class SubstitutionAuditLogConfiguration : IEntityTypeConfiguration<SubstitutionAuditLog>
{
    public void Configure(EntityTypeBuilder<SubstitutionAuditLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicSubstitutionAuditLogs", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => new { x.SubstitutionRequestId, x.Timestamp });
        builder.Property(x => x.ActorName).HasMaxLength(256);
        builder.Property(x => x.Action).HasMaxLength(256);
        builder.Property(x => x.Details).HasMaxLength(2000);
    }
}

public class SubstitutionNotificationLogConfiguration : IEntityTypeConfiguration<SubstitutionNotificationLog>
{
    public void Configure(EntityTypeBuilder<SubstitutionNotificationLog> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicSubstitutionNotificationLogs", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => new { x.SubstitutionRequestId, x.CreationTime });
        builder.Property(x => x.EventName).HasMaxLength(128);
        builder.Property(x => x.RecipientName).HasMaxLength(256);
        builder.Property(x => x.Channel).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(64);
        builder.Property(x => x.Details).HasMaxLength(1000);
    }
}

public class SubstitutionSettingConfiguration : IEntityTypeConfiguration<SubstitutionSetting>
{
    public void Configure(EntityTypeBuilder<SubstitutionSetting> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicSubstitutionSettings", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId });
    }
}
