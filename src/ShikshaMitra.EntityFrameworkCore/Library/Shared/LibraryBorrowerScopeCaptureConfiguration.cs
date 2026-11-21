using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.Library.Shared;

public sealed class LibraryBorrowerScopeCaptureConfiguration : IEntityTypeConfiguration<LibraryBorrowerScopeCapture>
{
	public void Configure(EntityTypeBuilder<LibraryBorrowerScopeCapture> builder)
	{
		builder.ToTable("AppLibraryBorrowerScopeCaptures", ShikshaMitraConsts.DbSchema, table =>
		{
			table.HasCheckConstraint("CK_LibraryBorrowerScopeCaptures_Source", "\"SourceKind\" IN (1, 2) AND \"Version\" > 0 AND \"Provenance\" IN (1, 2)");
			table.HasCheckConstraint("CK_LibraryBorrowerScopeCaptures_AssignmentCount", "\"ExpectedAssignmentCount\" >= 0");
			table.HasCheckConstraint("CK_LibraryBorrowerScopeCaptures_Actor", "(\"ActorKind\" = 1 AND \"ActorId\" IS NOT NULL AND \"SystemPurpose\" IS NULL) OR (\"ActorKind\" = 2 AND \"ActorId\" IS NULL AND \"SystemPurpose\" IS NOT NULL AND \"SystemPurpose\" IN (1, 2))");
		});
		// ABP makes IMultiTenant.TenantId optional, which conflicts with this school-only alternate key.
		builder.Property(row => row.TenantId).IsRequired().HasColumnName(nameof(LibraryBorrowerScopeCapture.TenantId));
		builder.Property(row => row.SourceName).HasMaxLength(80).IsRequired();
		builder.Property(row => row.IncompleteReason).HasMaxLength(500);
		builder.Property(row => row.AuditJson).HasColumnType("text");
		builder.HasAlternateKey(row => new { row.TenantId, row.Id });
		builder.HasIndex(row => new { row.TenantId, row.SourceKind, row.SourceId, row.Version }).IsUnique();
		builder.HasMany(row => row.Assignments).WithOne().HasForeignKey(row => new { row.TenantId, row.CaptureId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.Navigation(row => row.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
