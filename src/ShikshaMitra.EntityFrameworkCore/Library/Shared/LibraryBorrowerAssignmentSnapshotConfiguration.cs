using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Shared;

public sealed class LibraryBorrowerAssignmentSnapshotConfiguration : IEntityTypeConfiguration<LibraryBorrowerAssignmentSnapshot>
{
	public void Configure(EntityTypeBuilder<LibraryBorrowerAssignmentSnapshot> builder)
	{
		builder.ToTable("AppLibraryBorrowerAssignmentSnapshots", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.CaptureId, row.OrganizationAssignmentId }).IsUnique();
		builder.HasIndex(row => new { row.TenantId, row.CaptureId, row.AcademicYearId, row.StageId });
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
