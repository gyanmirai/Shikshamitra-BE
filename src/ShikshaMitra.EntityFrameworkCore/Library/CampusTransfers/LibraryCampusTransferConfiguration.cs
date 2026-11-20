using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.CampusTransfers;

public sealed class LibraryCampusTransferConfiguration : IEntityTypeConfiguration<LibraryCampusTransfer>
{
	public void Configure(EntityTypeBuilder<LibraryCampusTransfer> builder)
	{
		builder.ToTable("AppLibraryCampusTransfers", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.SourceCampus).HasMaxLength(AdmissionCampusConsts.MaxNameLength);
		builder.Property(row => row.DestinationCampus).HasMaxLength(AdmissionCampusConsts.MaxNameLength);
		builder.Property(row => row.SourceSection).HasMaxLength(128);
		builder.Property(row => row.DestinationSection).HasMaxLength(128);
		builder.Property(row => row.DestinationShelf).HasMaxLength(128);
		builder.Property(row => row.Reason).HasMaxLength(500);
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100);
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64);
		builder.HasIndex(row => new { row.TenantId, row.ActorId, row.IdempotencyKey }).IsUnique();
		builder.HasIndex(row => new { row.TenantId, row.RecordedAt, row.Id });
		builder.HasIndex(row => new { row.TenantId, row.DestinationSectionId });
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}

public sealed class LibraryCampusTransferReceiptConfiguration : IEntityTypeConfiguration<LibraryCampusTransferReceipt>
{
	public void Configure(EntityTypeBuilder<LibraryCampusTransferReceipt> builder)
	{
		builder.ToTable("AppLibraryCampusTransferReceipts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Notes).HasMaxLength(500);
		builder.Property(row => row.IdempotencyKey).HasMaxLength(100);
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64);
		builder.HasIndex(row => new { row.TenantId, row.TransferId, row.ActorId, row.IdempotencyKey }).IsUnique();
		builder.HasOne<LibraryCampusTransfer>().WithMany().HasForeignKey(row => new { row.TenantId, row.TransferId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}

public sealed class LibraryCampusTransferItemConfiguration : IEntityTypeConfiguration<LibraryCampusTransferItem>
{
	public void Configure(EntityTypeBuilder<LibraryCampusTransferItem> builder)
	{
		builder.ToTable("AppLibraryCampusTransferItems", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Title).HasMaxLength(256);
		builder.Property(row => row.AccessionNumber).HasMaxLength(BookCopy.MaximumAccessionLength);
		builder.HasIndex(row => new { row.TenantId, row.CopyId }).IsUnique().HasFilter("\"ReceiptId\" IS NULL");
		builder.HasIndex(row => new { row.TenantId, row.TransferId, row.CopyId }).IsUnique();
		builder.HasIndex(row => row.DispatchEventId).IsUnique();
		builder.HasIndex(row => row.ReceiptEventId).IsUnique();
		builder.HasOne<LibraryCampusTransfer>().WithMany().HasForeignKey(row => new { row.TenantId, row.TransferId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryCampusTransferReceipt>().WithMany().HasForeignKey(row => row.ReceiptId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookCopy>().WithMany().HasForeignKey(row => row.CopyId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryCopyEvent>().WithMany().HasForeignKey(row => row.DispatchEventId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryCopyEvent>().WithMany().HasForeignKey(row => row.ReceiptEventId).OnDelete(DeleteBehavior.Restrict);
		foreach (var property in builder.Metadata.GetProperties())
			if (property.Name is not (nameof(LibraryCampusTransferItem.ReceiptId) or nameof(LibraryCampusTransferItem.ReceiptEventId) or nameof(LibraryCampusTransferItem.Condition)))
				property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
