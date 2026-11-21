using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Reservations;

public sealed class LibraryHoldConfiguration : IEntityTypeConfiguration<LibraryHold>
{
	public void Configure(EntityTypeBuilder<LibraryHold> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryHolds", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TimeZoneId).HasMaxLength(120);
		builder.Property(row => row.EndReason).HasMaxLength(500);
		builder.Property(row => row.PolicySnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(row => row.CalendarSnapshotJson).HasColumnType("text").IsRequired();
		builder.HasOne<BookReservation>().WithMany().HasForeignKey(row => row.ReservationId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Book>().WithMany().HasForeignKey(row => row.BookId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookCopy>().WithMany().HasForeignKey(row => row.CopyId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<BookIssue>().WithMany().HasForeignKey(row => row.RelatedLoanId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.CopyId }).IsUnique().AreNullsDistinct(false).HasFilter("\"State\" = 0");
		builder.HasIndex(row => new { row.TenantId, row.ReservationId }).IsUnique().AreNullsDistinct(false).HasFilter("\"State\" = 0");
	}
}
