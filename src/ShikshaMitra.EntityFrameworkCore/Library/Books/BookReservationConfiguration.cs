using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Shared;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public class BookReservationConfiguration : IEntityTypeConfiguration<BookReservation>
{
    public void Configure(EntityTypeBuilder<BookReservation> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BookReservations", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
		builder.HasOne<LibraryBorrowerScopeCapture>().WithMany().HasForeignKey(row => new { row.TenantId, row.BorrowerScopeCaptureId })
			.HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.BookId)
            .IsRequired();

        builder.Property(x => x.MemberId)
            .IsRequired();

        builder.Property(x => x.RequestedDate).IsRequired(false);
        builder.Property(x => x.ReserveDueDate).IsRequired(false);
        builder.Property(x => x.Revision).HasMaxLength(128).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.ReviewReason).HasMaxLength(500);
        builder.Property(x => x.TerminalReason).HasMaxLength(500);
		builder.Property(x => x.PickupLabel).HasMaxLength(384);
		builder.HasIndex(x => new { x.TenantId, x.PickupSectionId, x.State }).HasFilter("\"PickupSectionId\" IS NOT NULL");
        builder.Property(x => x.PolicySnapshotJson).HasColumnType("text");
        builder.Property(x => x.BorrowerBoardId).IsRequired(false);
        builder.Property(x => x.BorrowerMediumId).IsRequired(false);
        builder.Property(x => x.BorrowerStageId).IsRequired(false);
        builder.Property(x => x.BorrowerStandardId).IsRequired(false);
        builder.Property(x => x.BorrowerDivisionId).IsRequired(false);
        builder.Property(x => x.BorrowerScopeSnapshotJson).HasColumnType("text");
        builder.Property(x => x.BorrowerScopeProvenance).HasDefaultValue(Shared.LibraryBorrowerScopeProvenance.Unknown);
        builder.HasIndex(x => new { x.TenantId, x.QueueSequence }).IsUnique().AreNullsDistinct(false)
            .HasFilter("\"QueueSequence\" IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.BookId, x.CanonicalMemberId }).IsUnique().AreNullsDistinct(false)
            .HasFilter("\"State\" IN (0, 1) AND \"CanonicalMemberId\" IS NOT NULL");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BookId);
        builder.HasIndex(x => x.MemberId);
    }
}
