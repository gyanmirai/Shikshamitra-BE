using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Security.UserScope;

/// <summary>
/// EF Core configuration for UserScopeAssignment entity.
/// </summary>
public class UserScopeAssignmentConfiguration : IEntityTypeConfiguration<UserScopeAssignment>
{
    public void Configure(EntityTypeBuilder<UserScopeAssignment> builder)
    {
        builder.ToTable("UserScopeAssignments");

        // Configure as ABP entity
        builder.ConfigureByConvention();

        // Primary Key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.TenantId)
            .IsRequired(false);

        builder.Property(x => x.UserId)
            .IsRequired(false);

        builder.Property(x => x.RoleName)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Nullable scope hierarchy
        builder.Property(x => x.AcademicYearId).IsRequired(false);
        builder.Property(x => x.EducationBoardId).IsRequired(false);
        builder.Property(x => x.EducationMediumId).IsRequired(false);
        builder.Property(x => x.EducationMediumStageId).IsRequired(false);
        builder.Property(x => x.StandardId).IsRequired(false);
        builder.Property(x => x.DivisionId).IsRequired(false);

        // Indexes for performance
        // Capture the index builder to avoid duplicate index creation
        var lookupIndex = builder.HasIndex(x => new { x.TenantId, x.UserId, x.IsActive, x.IsDeleted })
            .HasDatabaseName("IX_UserScopeAssignments_Lookup");

        // Use Npgsql-specific include properties on the same index
        lookupIndex.IncludeProperties(x => new
        {
            x.EducationBoardId,
            x.EducationMediumId,
            x.EducationMediumStageId,
            x.StandardId,
            x.DivisionId
        });

        builder.HasIndex(x => new { x.TenantId, x.RoleName, x.IsActive, x.IsDeleted })
            .HasDatabaseName("IX_UserScopeAssignments_Role");

        // Unique constraint to prevent duplicate assignments
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.UserId,
            x.RoleName,
            x.AcademicYearId,
            x.EducationBoardId,
            x.EducationMediumId,
            x.EducationMediumStageId,
            x.StandardId,
            x.DivisionId
        })
        .IsUnique()
        .HasDatabaseName("UQ_UserScopeAssignment")
        .HasFilter("\"IsDeleted\" = false"); // PostgreSQL syntax: use double quotes and 'false' instead of 0

        // Foreign Keys (optional - can be added later if needed for referential integrity)
        // Commenting out for now as relationships may not be configured yet

        /*
        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AcademicYear>()
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EducationBoard>()
            .WithMany()
            .HasForeignKey(x => x.EducationBoardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EducationMedium>()
            .WithMany()
            .HasForeignKey(x => x.EducationMediumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EducationMediumStage>()
            .WithMany()
            .HasForeignKey(x => x.EducationMediumStageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Standard>()
            .WithMany()
            .HasForeignKey(x => x.StandardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Division>()
            .WithMany()
            .HasForeignKey(x => x.DivisionId)
            .OnDelete(DeleteBehavior.Restrict);
        */
    }
}
