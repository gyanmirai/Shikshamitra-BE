using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Reservations;

public sealed class LibraryReservationEventConfiguration : IEntityTypeConfiguration<LibraryReservationEvent>
{
	public void Configure(EntityTypeBuilder<LibraryReservationEvent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryReservationEvents", ShikshaMitraConsts.DbSchema,
			table => table.HasCheckConstraint("CK_LibraryReservationEvents_Actor", "(\"ActorKind\" = 1 AND \"ActorId\" IS NOT NULL AND \"SystemPurpose\" IS NULL) OR (\"ActorKind\" = 2 AND \"ActorId\" IS NULL AND \"SystemPurpose\" IS NOT NULL AND \"SystemPurpose\" IN (1, 2))"));
		builder.ConfigureByConvention();
		builder.Property(row => row.EventType).HasMaxLength(60).IsRequired();
		builder.Property(row => row.Reason).HasMaxLength(500).IsRequired();
		builder.Property(row => row.BeforeSnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(row => row.AfterSnapshotJson).HasColumnType("text").IsRequired();
		builder.HasOne<BookReservation>().WithMany().HasForeignKey(row => row.ReservationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryHold>().WithMany().HasForeignKey(row => row.HoldId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookCopy>().WithMany().HasForeignKey(row => row.CopyId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookIssue>().WithMany().HasForeignKey(row => row.LoanId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryBorrowerScopeCapture>().WithMany().HasForeignKey(row => new { row.TenantId, row.BeforeBorrowerScopeCaptureId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<LibraryBorrowerScopeCapture>().WithMany().HasForeignKey(row => new { row.TenantId, row.AfterBorrowerScopeCaptureId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.ReservationId, row.Sequence }).IsUnique().AreNullsDistinct(false);
		foreach (var property in builder.Metadata.GetProperties()) property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
	}
}
