using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.BookManagement.AddNewBooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.BookManagement;

public class AddNewBookConfiguration : IEntityTypeConfiguration<AddNewBook>
{
	public void Configure(EntityTypeBuilder<AddNewBook> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AddNewBooks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention(); 

		builder.Property(b => b.TenantId)
			   .IsRequired(false);

		builder.Property(b => b.BookTitle)
			   .IsRequired()
			   .HasMaxLength(128);

		builder.Property(b => b.BookAuthor)
			   .IsRequired()
			   .HasMaxLength(128);

		builder.Property(b => b.ISBN)
			   .IsRequired()
			   .HasMaxLength(20);

		builder.Property(b => b.Publisher)
			   .HasMaxLength(128);

		builder.Property(b => b.Year)
			   .IsRequired();

		builder.Property(b => b.AvailableCopies)
			   .IsRequired();

		builder.Property(b => b.Category)
			   .HasMaxLength(64);

		builder.Property(b => b.Condition)
			   .HasMaxLength(64);

		builder.Property(b => b.PicturePath)
			   .HasMaxLength(256);

		builder.Property(b => b.EditionNotes)
			   .HasMaxLength(256);

		builder.HasIndex(b => b.ISBN).IsUnique(); // Unique constraint on ISBN
	}
}
