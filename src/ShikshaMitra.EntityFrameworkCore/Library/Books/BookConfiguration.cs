using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Books;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
	public void Configure(EntityTypeBuilder<Book> builder)
	{
		builder.ToTable("Books");

		builder.HasKey(b => b.Id);

		builder.Property(b => b.TenantId)
			.IsRequired(false);

		builder.Property(b => b.BookTitle)
			.IsRequired()
			.HasMaxLength(256);

		builder.Property(b => b.BookAuthor)
			.IsRequired()
			.HasMaxLength(256);

		builder.Property(b => b.ISBN)
			.HasMaxLength(50);

		builder.Property(b => b.Publisher)
			.HasMaxLength(256);

		builder.Property(b => b.Year)
			.IsRequired();

		builder.Property(b => b.AvailableCopies)
			.IsRequired();

		builder.Property(b => b.BookCategoryId)
			.HasMaxLength(100);

		builder.Property(b => b.PicturePath)
			.HasMaxLength(512);

		builder.Property(b => b.EditionNotes)
			.HasMaxLength(512);

		builder.HasOne<BookCategory>()
			.WithMany()
			.HasForeignKey(b => b.BookCategoryId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
