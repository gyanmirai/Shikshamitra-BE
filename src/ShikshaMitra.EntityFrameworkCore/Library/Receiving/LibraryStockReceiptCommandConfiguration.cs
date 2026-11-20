using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockReceiptCommandConfiguration : IEntityTypeConfiguration<LibraryStockReceiptCommand>
{
	public void Configure(EntityTypeBuilder<LibraryStockReceiptCommand> builder)
	{
		builder.ToTable("AppLibraryStockReceiptCommands", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100).IsRequired();
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.ActorId, row.IdempotencyKey }).IsUnique();
		builder.HasIndex(row => row.ReceiptId).IsUnique();
		builder.HasOne<LibraryStockReceipt>().WithMany().HasForeignKey(row => new { row.TenantId, row.ReceiptId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
