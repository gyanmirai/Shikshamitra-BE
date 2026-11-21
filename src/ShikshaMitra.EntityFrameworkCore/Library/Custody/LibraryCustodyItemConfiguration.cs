using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Custody;

public sealed class LibraryCustodyItemConfiguration : IEntityTypeConfiguration<LibraryCustodyItem>
{
	public void Configure(EntityTypeBuilder<LibraryCustodyItem> builder)
	{
		builder.ToTable("AppLibraryCustodyItems", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Title).HasMaxLength(256).IsRequired();
		builder.Property(row => row.AccessionNumber).HasMaxLength(BookCopy.MaximumAccessionLength).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.CopyId }).IsUnique().HasFilter("\"ReturnReceiptId\" IS NULL");
		builder.HasIndex(row => new { row.TenantId, row.HandoffId, row.CopyId }).IsUnique();
		builder.HasIndex(row => row.HandoffEventId).IsUnique();
		builder.HasIndex(row => row.ReturnEventId).IsUnique();
		builder.HasOne<LibraryCustodyHandoff>().WithMany().HasForeignKey(row => new { row.TenantId, row.HandoffId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryCustodyReceipt>().WithMany().HasForeignKey(row => row.ReturnReceiptId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookCopy>().WithMany().HasForeignKey(row => row.CopyId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryCopyEvent>().WithMany().HasForeignKey(row => row.HandoffEventId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryCopyEvent>().WithMany().HasForeignKey(row => row.ReturnEventId).OnDelete(DeleteBehavior.Restrict);
		foreach (var property in builder.Metadata.GetProperties())
			if (property.Name is not (nameof(LibraryCustodyItem.ReturnReceiptId) or nameof(LibraryCustodyItem.ReturnEventId) or nameof(LibraryCustodyItem.ReturnedCondition)))
				property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
