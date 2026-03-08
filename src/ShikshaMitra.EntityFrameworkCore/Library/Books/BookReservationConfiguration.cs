using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public class BookReservationConfiguration : IEntityTypeConfiguration<BookReservation>
{
    public void Configure(EntityTypeBuilder<BookReservation> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BookReservations", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.BookId)
            .IsRequired();

        builder.Property(x => x.MemberId)
            .IsRequired();

        builder.Property(x => x.RequestedDate)
            .IsRequired();

        builder.Property(x => x.ReserveDueDate)
            .IsRequired();

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BookId);
        builder.HasIndex(x => x.MemberId);
    }
}
