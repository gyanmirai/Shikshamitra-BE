using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.Sections;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockReceiptLineConfiguration : IEntityTypeConfiguration<LibraryStockReceiptLine>
{
	public void Configure(EntityTypeBuilder<LibraryStockReceiptLine> builder)
	{
		builder.ToTable("AppLibraryStockReceiptLines", ShikshaMitraConsts.DbSchema, table =>
		{
			table.HasCheckConstraint("CK_LibraryStockReceiptLines_Count", "\"ExpectedCopyCount\" BETWEEN 1 AND 5000");
			table.HasCheckConstraint("CK_LibraryStockReceiptLines_Number", "\"LineNumber\" BETWEEN 1 AND 100");
			table.HasCheckConstraint("CK_LibraryStockReceiptLines_Cost", "(\"UnitCost\" IS NULL AND \"Currency\" IS NULL) OR (\"UnitCost\" IS NOT NULL AND \"Currency\" IS NOT NULL AND length(\"Currency\") = 3 AND \"UnitCost\" >= 0 AND \"UnitCost\" <= 9999999999999999.99 AND \"UnitCost\" = round(\"UnitCost\", 2))");
		});
		// Keep the tenant dimension required before configuring same-school child keys.
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Currency).HasMaxLength(3);
		// Keep unrestricted numeric storage so a sub-paise SQL input reaches the check instead of being rounded on assignment.
		builder.Property(row => row.UnitCost).HasColumnType("numeric");
		builder.HasAlternateKey(row => new { row.TenantId, row.Id });
		builder.HasIndex(row => new { row.ReceiptId, row.LineNumber }).IsUnique();
		builder.HasOne<Book>().WithMany().HasForeignKey(row => row.BookId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibrarySection>().WithMany().HasForeignKey(row => row.SectionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryShelf>().WithMany().HasForeignKey(row => row.ShelfId).OnDelete(DeleteBehavior.Restrict);
		builder.HasMany(row => row.Copies).WithOne().HasForeignKey(row => new { row.TenantId, row.ReceiptLineId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.Navigation(row => row.Copies).UsePropertyAccessMode(PropertyAccessMode.Field);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
