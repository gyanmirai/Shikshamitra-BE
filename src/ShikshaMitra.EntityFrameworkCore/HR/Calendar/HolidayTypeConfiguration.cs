using ShikshaMitra.HR.Calendar.HolidayTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore;

/// <summary>
/// EF Core configuration for HolidayType entity
/// </summary>
public class HolidayTypeConfiguration : IEntityTypeConfiguration<HolidayType>
{
    public void Configure(EntityTypeBuilder<HolidayType> builder)
    {
        builder.ToTable("AppHolidayTypes");

        builder.ConfigureByConvention();

        // Properties
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Color)
            .IsRequired()
            .HasMaxLength(7); // #RRGGBB format

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // Indices for common queries
        builder.HasIndex(x => new { x.Name, x.TenantId })
            .IsUnique();

        builder.HasIndex(x => x.TenantId);
    }
}
