using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Groups;

namespace ShikshaMitra.EntityFrameworkCore.Groups;

/// <summary>
/// EF Core configuration for Group entity
/// </summary>
public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable($"{ShikshaMitraConsts.DbTablePrefix}Groups");

        builder.HasKey(x => x.Id);

        // Required properties
        builder.Property(x => x.AcademicYearId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.SelectionMode)
            .IsRequired();

        // Optional properties
        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // JSON conversion for List<Guid> properties
        builder.Property(x => x.SelectedDivisionIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => string.IsNullOrEmpty(v) ? new List<System.Guid>() : JsonSerializer.Deserialize<List<System.Guid>>(v, JsonSerializerOptions.Default) ?? new List<System.Guid>())
            .HasColumnType("jsonb");

        builder.Property(x => x.RoleIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => string.IsNullOrEmpty(v) ? new List<System.Guid>() : JsonSerializer.Deserialize<List<System.Guid>>(v, JsonSerializerOptions.Default) ?? new List<System.Guid>())
            .HasColumnType("jsonb");

        builder.Property(x => x.TenantId);

        // One-to-many relationships
        builder.HasMany(x => x.StudentRules)
            .WithOne()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.EmployeeRules)
            .WithOne()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to AcademicYear
        builder.HasOne<ShikshaMitra.TenantSettings.AcademicYears.AcademicYear>()
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indices for performance
        builder.HasIndex(x => new { x.AcademicYearId, x.TenantId })
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Groups_AcademicYearId_Tenant");

        builder.HasIndex(x => new { x.Name, x.TenantId })
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Groups_Name_Tenant");

        builder.HasIndex(x => x.SelectionMode)
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Groups_SelectionMode");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName($"{ShikshaMitraConsts.DbTablePrefix}Groups_Tenant");
    }
}
