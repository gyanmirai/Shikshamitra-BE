using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationResultConfiguration : IEntityTypeConfiguration<LibraryStockVerificationResult>
{
	public void Configure(EntityTypeBuilder<LibraryStockVerificationResult> builder)
	{
		builder.ToTable("AppLibraryStockVerificationResults", ShikshaMitraConsts.DbSchema, table =>
		{
			table.HasCheckConstraint("CK_LibraryStockResult_Position", "\"Position\" > 0");
			table.HasCheckConstraint("CK_LibraryStockResult_Source", "\"Source\" IN (1, 2, 3)");
			table.HasCheckConstraint("CK_LibraryStockResult_Finding", "\"Finding\" BETWEEN 1 AND 17");
			table.HasCheckConstraint("CK_LibraryStockResult_ReceiptSource", "NOT \"ReceivedAfterOpening\" OR \"Source\" = 2");
			table.HasCheckConstraint("CK_LibraryStockResult_Decision", "(\"Finding\" IN (2, 3, 4, 10, 12, 13, 14, 15)) = (\"DecisionId\" IS NOT NULL)");
		});
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.AccessionNumber).HasMaxLength(BookCopy.MaximumAccessionLength);
		builder.Property(row => row.EvidenceRevision).HasMaxLength(64);
		builder.HasOne<LibraryStockVerificationCommand>().WithMany().HasForeignKey(row => new { row.TenantId, row.SessionId, row.CommandId })
			.HasPrincipalKey(row => new { row.TenantId, row.SessionId, row.Id }).HasConstraintName("FK_LibraryStockResult_Command")
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryStockVerificationDecision>().WithMany().HasForeignKey(row => new { row.TenantId, row.SessionId, row.DecisionId })
			.HasPrincipalKey(row => new { row.TenantId, row.SessionId, row.Id }).HasConstraintName("FK_LibraryStockResult_Decision")
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.Source, row.RowId }).IsUnique()
			.HasDatabaseName("UX_LibraryStockResult_Tenant_Session_Source_Row");
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.Position }).IsUnique()
			.HasDatabaseName("UX_LibraryStockResult_Tenant_Session_Position");
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
