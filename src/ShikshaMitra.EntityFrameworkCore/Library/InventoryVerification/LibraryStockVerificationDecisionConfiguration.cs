using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationDecisionConfiguration : IEntityTypeConfiguration<LibraryStockVerificationDecision>
{
	public void Configure(EntityTypeBuilder<LibraryStockVerificationDecision> builder)
	{
		builder.ToTable("AppLibraryStockVerificationDecisions", ShikshaMitraConsts.DbSchema, table =>
		{
			table.HasCheckConstraint("CK_LibraryStockDecision_Version", "\"Version\" > 0");
			table.HasCheckConstraint("CK_LibraryStockDecision_Source", "\"Source\" IN (1, 2, 3)");
			table.HasCheckConstraint("CK_LibraryStockDecision_Outcome", "\"Outcome\" IN (1, 2, 3)");
		});
		builder.Property(row => row.TenantId).IsRequired();
		builder.HasAlternateKey(row => new { row.TenantId, row.SessionId, row.Id }).HasName("AK_LibraryStockDecision_Tenant_Session_Id");
		builder.Property(row => row.AccessionNumber).HasMaxLength(BookCopy.MaximumAccessionLength);
		builder.Property(row => row.EvidenceRevision).HasMaxLength(64);
		builder.Property(row => row.Reason).HasMaxLength(LibraryStockVerificationDecision.MaximumReasonLength);
		builder.HasOne<LibraryStockVerificationCommand>().WithMany().HasForeignKey(row => new { row.TenantId, row.SessionId, row.CommandId })
			.HasPrincipalKey(row => new { row.TenantId, row.SessionId, row.Id }).HasConstraintName("FK_LibraryStockDecision_Command")
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.CommandId }).IsUnique()
			.HasDatabaseName("UX_LibraryStockDecision_Tenant_Session_Command");
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.Source, row.RowId, row.Version }).IsUnique()
			.HasDatabaseName("UX_LibraryStockDecision_Tenant_Session_Row_Version");
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
