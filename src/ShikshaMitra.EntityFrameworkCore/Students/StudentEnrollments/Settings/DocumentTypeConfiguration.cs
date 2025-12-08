using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Settings;

/// <summary>
/// EF Core configuration for DocumentType entity
/// </summary>
public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
	public void Configure(EntityTypeBuilder<DocumentType> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "DocumentTypes", ShikshaMitraConsts.DbSchema);

		// Configure default ABP properties
		builder.ConfigureByConvention();

		// Properties
		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(DocumentTypeConsts.MaxNameLength);

		builder.Property(x => x.MaxSizeMB)
			.IsRequired()
			.HasColumnType("decimal(10,2)");

		// Store enum list as comma-separated string
		builder.Property(x => x.AcceptedFormatsData)
			.IsRequired()
			.HasMaxLength(200) // e.g., "1,2,3,4,5,6"
			.HasColumnName("AcceptedFormats");

		builder.Property(x => x.IsRequired)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(x => x.DisplayOrder)
			.IsRequired()
			.HasDefaultValue(0);

		// Indexes
		builder.HasIndex(x => new { x.TenantId, x.Name })
			.IsUnique()
			.HasDatabaseName("IX_DocumentTypes_TenantId_Name");

		builder.HasIndex(x => new { x.TenantId, x.DisplayOrder })
			.HasDatabaseName("IX_DocumentTypes_TenantId_DisplayOrder");
	}
}
