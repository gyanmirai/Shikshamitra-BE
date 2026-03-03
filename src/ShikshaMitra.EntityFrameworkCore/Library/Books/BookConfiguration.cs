using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Books;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Books", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Isbn)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Author)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Publisher)
            .HasMaxLength(256);

        builder.Property(x => x.Edition)
            .HasMaxLength(50);

        builder.Property(x => x.Subject)
            .HasMaxLength(128);

        builder.Property(x => x.Language)
            .IsRequired();

        builder.Property(x => x.ApplicableAge)
            .HasMaxLength(10);

        builder.Property(x => x.Price)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.AcademicYearId);

        builder.HasIndex(x => new { x.Isbn, x.Edition });
    }
}
