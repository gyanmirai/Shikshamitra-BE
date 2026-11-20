using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockImportBatchConfiguration : IEntityTypeConfiguration<LibraryStockImportBatch>
{
	public void Configure(EntityTypeBuilder<LibraryStockImportBatch> builder)
	{
		builder.ToTable("AppLibraryStockImportBatches", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100).IsRequired();
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(row => row.SourceName).HasMaxLength(256);
		builder.HasIndex(row => new { row.TenantId, row.ImportedBy, row.IdempotencyKey }).IsUnique();
		builder.HasIndex(row => new { row.TenantId, row.AcademicYearId, row.ImportedAt, row.Id });
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}

public sealed class LibraryStockImportRowConfiguration : IEntityTypeConfiguration<LibraryStockImportRow>
{
	public void Configure(EntityTypeBuilder<LibraryStockImportRow> builder)
	{
		builder.ToTable("AppLibraryStockImportRows", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.SourceRowId).HasMaxLength(100).IsRequired();
		builder.Property(row => row.NormalizedSourceRowId).HasMaxLength(100).IsRequired();
		builder.Property(row => row.Title).HasMaxLength(256).IsRequired();
		builder.Property(row => row.AccessionNumber).HasMaxLength(50).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.NormalizedSourceRowId }).IsUnique();
		builder.HasIndex(row => row.CopyId).IsUnique();
		builder.HasIndex(row => new { row.TenantId, row.BatchId, row.RowNumber }).IsUnique();
		builder.HasOne<LibraryStockImportBatch>().WithMany().HasForeignKey(row => new { row.TenantId, row.BatchId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryStockReceipt>().WithMany().HasForeignKey(row => new { row.TenantId, row.ReceiptId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
