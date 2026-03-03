using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public class BookCopyConfiguration : IEntityTypeConfiguration<BookCopy>
{
    public void Configure(EntityTypeBuilder<BookCopy> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BookCopies", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.BookId)
            .IsRequired();

        builder.Property(x => x.CopyNumber)
            .IsRequired();

        builder.Property(x => x.AccessionNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SectionId)
            .IsRequired();

        builder.Property(x => x.ShelfId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasDefaultValue(BookCopyStatus.Available);

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.HasOne<Book>()
            .WithMany(b => b.Copies)
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.AccessionNumber).IsUnique();
        builder.HasIndex(x => x.BookId);
    }
}
