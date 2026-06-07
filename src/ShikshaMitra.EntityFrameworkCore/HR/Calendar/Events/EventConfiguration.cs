using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Calendar.Events;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Calendar.Events;

/// <summary>
/// EF Core configuration for Event entity
/// </summary>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("AppEvents");

        builder.ConfigureByConvention();

        // Property configuration
        builder.Property(x => x.AcademicYearId)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.EventTypeId)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Poster);

        builder.Property(x => x.Mode)
            .HasConversion<int>();

        builder.Property(x => x.Venue);

        builder.Property(x => x.MeetingDate)
            .IsRequired(false);

        builder.Property(x => x.StartDate)
            .IsRequired(false);

        builder.Property(x => x.EndDate)
            .IsRequired(false);

        builder.Property(x => x.StartTime)
            .IsRequired();

        builder.Property(x => x.EndTime)
            .IsRequired();

        builder.Property(x => x.IsRecurring)
            .HasDefaultValue(false);

        builder.Property(x => x.RecurrenceJson);

        builder.Property(x => x.AudienceIds)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, System.Text.Json.JsonSerializerOptions.Default),
                v => string.IsNullOrEmpty(v) ? new() : System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, System.Text.Json.JsonSerializerOptions.Default) ?? new())
                        .HasColumnType("jsonb");

        // Foreign key relationships
        builder.HasOne<ShikshaMitra.TenantSettings.AcademicYears.AcademicYear>()
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indices for performance
        builder.HasIndex(x => new { x.AcademicYearId, x.TenantId })
            .HasDatabaseName("IX_AppEvents_AcademicYearId_TenantId");

        builder.HasIndex(x => new { x.Title, x.TenantId })
            .HasDatabaseName("IX_AppEvents_Title_TenantId");

        builder.HasIndex(x => x.EventTypeId)
            .HasDatabaseName("IX_AppEvents_EventTypeId");

        builder.HasIndex(x => x.MeetingDate)
            .HasDatabaseName("IX_AppEvents_MeetingDate");

        builder.HasIndex(x => new { x.StartDate, x.EndDate })
            .HasDatabaseName("IX_AppEvents_StartDate_EndDate");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_AppEvents_TenantId");
    }
}
