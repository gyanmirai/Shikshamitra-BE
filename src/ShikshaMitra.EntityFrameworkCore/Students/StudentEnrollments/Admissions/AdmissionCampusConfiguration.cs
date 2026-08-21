using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

internal sealed class AdmissionCampusConfiguration : IEntityTypeConfiguration<AdmissionCampus>
{
    public void Configure(EntityTypeBuilder<AdmissionCampus> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCampuses", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(AdmissionCampusConsts.MaxCodeLength);
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(AdmissionCampusConsts.MaxNameLength);
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
        builder.ConfigureSoftDeleteDefault();

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .HasDatabaseName("UX_AdmissionCampuses_Tenant_Code")
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => new { x.TenantId, x.IsActive })
            .HasDatabaseName("IX_AdmissionCampuses_Tenant_Active")
            .HasFilter("\"IsDeleted\" = false");
    }
}
