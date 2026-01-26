using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Calendar.EventTypes;

public class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EventTypes", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        // Configure properties
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256)
            .HasComment("Name of the event type");

        builder.Property(x => x.Color)
            .IsRequired()
            .HasMaxLength(7) // Hex color max length
            .HasComment("Hex color code for UI representation");

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .HasComment("Optional description of the event type");

        builder.Property(x => x.TenantId)
            .HasComment("Tenant ID for multi-tenancy");

        // Add index for Name search (performance optimization)
        builder.HasIndex(x => new { x.Name, x.TenantId });

        // Add index for TenantId (automatic filtering in multi-tenant scenarios)
        builder.HasIndex(x => x.TenantId);
    }
}
