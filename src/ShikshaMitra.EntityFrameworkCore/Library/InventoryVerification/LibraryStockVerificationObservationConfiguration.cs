using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationObservationConfiguration : IEntityTypeConfiguration<LibraryStockVerificationObservation>
{
	public void Configure(EntityTypeBuilder<LibraryStockVerificationObservation> builder)
	{
		builder.ToTable("AppLibraryStockVerificationObservations", ShikshaMitraConsts.DbSchema, table =>
		{
			table.HasCheckConstraint("CK_LibraryStockVerificationObservation_Sequence", "\"Sequence\" > 0");
			table.HasCheckConstraint("CK_LibraryStockVerificationObservation_Location", "\"ObservedShelfId\" IS NULL OR \"ObservedSectionId\" IS NOT NULL");
		});
		// The tenant dimension also participates in the replay reference key.
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.AccessionNumber).HasMaxLength(BookCopy.MaximumAccessionLength);
		builder.Property(row => row.SchoolDate).HasColumnType("date");
		builder.HasAlternateKey(row => new { row.TenantId, row.SessionId, row.Id }).HasName("AK_LibraryStockObservation_Tenant_Session_Id");
		builder.HasOne<LibraryStockVerificationSession>().WithMany().HasForeignKey(row => new { row.TenantId, row.SessionId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).HasConstraintName("FK_LibraryStockVerificationObservation_Session").OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.Sequence })
			.IsUnique().HasDatabaseName("UX_LibraryStockVerificationObservation_Tenant_Session_Sequence");
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.CopyId, row.Sequence })
			.HasFilter("\"CopyId\" IS NOT NULL").HasDatabaseName("IX_LibraryStockObservation_Tenant_Session_Copy_Sequence");
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
