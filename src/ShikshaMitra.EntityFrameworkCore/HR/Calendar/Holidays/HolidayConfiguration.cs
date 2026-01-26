using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Calendar.Holidays;

namespace ShikshaMitra.EntityFrameworkCore.HR.Calendar.Holidays;

/// <summary>
/// EF Core configuration for Holiday entity
/// </summary>
public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable($"{ShikshaMitraConsts.DbTablePrefix}Holidays");

        builder.HasKey(x => x.Id);

        // Required properties
        builder.Property(x => x.HolidayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.HolidayTypeId)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate)
            .IsRequired();

        // Optional properties
        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.AudienceIds)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, System.Text.Json.JsonSerializerOptions.Default),
                v => string.IsNullOrEmpty(v) ? new() : System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, System.Text.Json.JsonSerializerOptions.Default) ?? new())
            .HasColumnType("jsonb");

        builder.Property(x => x.Attachments)
            .HasMaxLength(2000);

        builder.Property(x => x.TenantId);

        // Indices for performance
        builder.HasIndex(x => new { x.HolidayName, x.TenantId })
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Holidays_HolidayNameTenant");

        builder.HasIndex(x => x.HolidayTypeId)
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Holidays_TypeId");

        builder.HasIndex(x => new { x.StartDate, x.EndDate })
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Holidays_DateRange");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Holidays_Tenant");
    }
}
