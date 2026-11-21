using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockReceiptConfiguration : IEntityTypeConfiguration<LibraryStockReceipt>
{
	public void Configure(EntityTypeBuilder<LibraryStockReceipt> builder)
	{
		builder.ToTable("AppLibraryStockReceipts", ShikshaMitraConsts.DbSchema, table =>
		{
			table.HasCheckConstraint("CK_LibraryStockReceipts_Source", "\"Source\" IN (0, 1, 2, 3)");
			table.HasCheckConstraint("CK_LibraryStockReceipts_Counts", "\"ExpectedLineCount\" BETWEEN 1 AND 100 AND \"ExpectedCopyCount\" BETWEEN 1 AND 5000");
		});
		// The school-only alternate key requires TenantId; ABP's optional tenant convention conflicts with that key.
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.SourceName).HasMaxLength(256);
		builder.Property(row => row.Reference).HasMaxLength(256);
		builder.Property(row => row.ReceivedOn).HasColumnType("date");
		builder.HasAlternateKey(row => new { row.TenantId, row.Id });
		builder.HasIndex(row => new { row.TenantId, row.ReceivedOn, row.Id });
		builder.HasMany(row => row.Lines).WithOne().HasForeignKey(row => new { row.TenantId, row.ReceiptId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.Navigation(row => row.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
