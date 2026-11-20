using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationCommandConfiguration : IEntityTypeConfiguration<LibraryStockVerificationCommand>
{
	public void Configure(EntityTypeBuilder<LibraryStockVerificationCommand> builder)
	{
		builder.ToTable("AppLibraryStockVerificationCommands", ShikshaMitraConsts.DbSchema, table =>
			table.HasCheckConstraint("CK_LibraryStockVerificationCommand_Target", "(\"Kind\" IN (1, 3, 4, 5, 6) AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)"));
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.IdempotencyKey).HasMaxLength(LibraryStockVerificationCommand.MaximumKeyLength);
		builder.Property(row => row.PayloadFingerprint).HasMaxLength(64);
		builder.HasAlternateKey(row => new { row.TenantId, row.SessionId, row.Id }).HasName("AK_LibraryStockCommand_Tenant_Session_Id");
		builder.HasOne<LibraryStockVerificationSession>().WithMany().HasForeignKey(row => new { row.TenantId, row.SessionId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).HasConstraintName("FK_LibraryStockCommand_Session").OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryStockVerificationObservation>().WithMany().HasForeignKey(row => new { row.TenantId, row.SessionId, row.ObservationId })
			.HasPrincipalKey(row => new { row.TenantId, row.SessionId, row.Id }).HasConstraintName("FK_LibraryStockCommand_Observation").OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.ActorId, row.IdempotencyKey }).IsUnique()
			.HasDatabaseName("UX_LibraryStockCommand_Tenant_Actor_Key");
		builder.HasIndex(row => new { row.TenantId, row.SessionId, row.Kind }).IsUnique()
			.HasFilter("\"Kind\" IN (3, 4, 6)").HasDatabaseName("UX_LibraryStockCommand_Tenant_Session_Transition");
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
