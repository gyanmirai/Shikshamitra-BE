using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationSessionConfiguration : IEntityTypeConfiguration<LibraryStockVerificationSession>
{
	public void Configure(EntityTypeBuilder<LibraryStockVerificationSession> builder)
	{
		builder.ToTable("AppLibraryStockVerificationSessions", ShikshaMitraConsts.DbSchema, table =>
		{
			table.HasCheckConstraint("CK_LibraryStockVerificationSession_Counts", "\"ExpectedCopyCount\" >= 0 AND \"ObservationCount\" >= 0");
			table.HasCheckConstraint("CK_LibraryStockVerificationSession_Location", "\"SectionId\" IS NOT NULL OR (\"ShelfId\" IS NULL AND \"AcademicYearId\" IS NULL)");
			table.HasCheckConstraint("CK_LibraryStockVerificationSession_State", """
				("ReviewStartedBy" IS NULL) = ("ReviewStartedAt" IS NULL) AND
				("FinalizedBy" IS NULL) = ("FinalizedAt" IS NULL) AND
				("FinalizedBy" IS NULL) = ("FinalizedEvidenceFingerprint" IS NULL) AND
				("CancelledBy" IS NULL) = ("CancelledAt" IS NULL) AND
				("CancelledBy" IS NULL) = ("CancellationReason" IS NULL) AND
				(("Status" = 1 AND "ReviewStartedBy" IS NULL AND "FinalizedBy" IS NULL AND "CancelledBy" IS NULL) OR
				 ("Status" = 2 AND "ReviewStartedBy" IS NOT NULL AND "FinalizedBy" IS NULL AND "CancelledBy" IS NULL) OR
				 ("Status" = 3 AND "ReviewStartedBy" IS NOT NULL AND "FinalizedBy" IS NOT NULL AND "CancelledBy" IS NULL) OR
				 ("Status" = 4 AND "FinalizedBy" IS NULL AND "CancelledBy" IS NOT NULL))
				""");
		});
		// Configure auditing without the optional tenant convention for this required composite key.
		builder.ConfigureFullAuditedAggregateRoot();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Name).HasMaxLength(LibraryStockVerificationSession.MaximumNameLength);
		builder.Property(row => row.CancellationReason).HasMaxLength(LibraryStockVerificationSession.MaximumCancellationReasonLength);
		builder.Property(row => row.FinalizedEvidenceFingerprint).HasMaxLength(64);
		builder.Property(row => row.SchoolDate).HasColumnType("date");
		builder.HasAlternateKey(row => new { row.TenantId, row.Id }).HasName("AK_LibraryStockVerificationSession_Tenant_Id");
		builder.HasIndex(row => new { row.TenantId, row.SchoolDate, row.Id }).HasDatabaseName("IX_LibraryStockVerificationSession_Tenant_Date");
		foreach (var immutable in new[] { nameof(LibraryStockVerificationSession.Name), nameof(LibraryStockVerificationSession.SectionId),
			nameof(LibraryStockVerificationSession.ShelfId), nameof(LibraryStockVerificationSession.AcademicYearId), nameof(LibraryStockVerificationSession.SchoolDate),
			nameof(LibraryStockVerificationSession.ExpectedCopyCount), nameof(LibraryStockVerificationSession.OpenedBy),
			nameof(LibraryStockVerificationSession.OpenedAt) })
			builder.Property(immutable).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
