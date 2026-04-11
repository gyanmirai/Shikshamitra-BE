using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Communication.AnnouncementTypes;

namespace ShikshaMitra.EntityFrameworkCore.Communication.AnnouncementTypes;

/// <summary>
/// EF Core configuration for AnnouncementType entity
/// </summary>
public class AnnouncementTypeConfiguration : IEntityTypeConfiguration<AnnouncementType>
{
    public void Configure(EntityTypeBuilder<AnnouncementType> builder)
    {
        builder.ToTable("AnnouncementTypes");

        builder.Property(x => x.AnnouncementTypeName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Color)
            .HasMaxLength(20);

        builder.HasIndex(x => new { x.TenantId, x.AcademicMediumId, x.AnnouncementTypeName })
            .HasDatabaseName("IX_AnnouncementTypes_Tenant_Medium_Name");
    }
}
