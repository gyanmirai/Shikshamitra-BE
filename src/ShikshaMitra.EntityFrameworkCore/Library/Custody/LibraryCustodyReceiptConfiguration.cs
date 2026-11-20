using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Custody;

public sealed class LibraryCustodyReceiptConfiguration : IEntityTypeConfiguration<LibraryCustodyReceipt>
{
	public void Configure(EntityTypeBuilder<LibraryCustodyReceipt> builder)
	{
		builder.ToTable("AppLibraryCustodyReceipts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Notes).HasMaxLength(500);
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100).IsRequired();
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.ActorId, row.IdempotencyKey }).IsUnique();
		builder.HasOne<LibraryCustodyHandoff>().WithMany().HasForeignKey(row => new { row.TenantId, row.HandoffId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
