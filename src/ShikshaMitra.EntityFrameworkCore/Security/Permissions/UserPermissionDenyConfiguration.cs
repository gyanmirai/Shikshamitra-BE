using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Security.Permissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Security.Permissions;

public class UserPermissionDenyConfiguration : IEntityTypeConfiguration<UserPermissionDeny>
{
    public void Configure(EntityTypeBuilder<UserPermissionDeny> builder)
    {
        builder.ToTable("UserPermissionDenies");
        builder.ConfigureByConvention();
        builder.Property(row => row.PermissionName).HasMaxLength(256).IsRequired();
        builder.HasIndex(row => new { row.TenantId, row.UserId, row.PermissionName }).IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"TenantId\" IS NOT NULL");
        builder.HasIndex(row => new { row.UserId, row.PermissionName }).IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"TenantId\" IS NULL");
    }
}
