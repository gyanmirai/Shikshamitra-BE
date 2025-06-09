using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BooksReservations;

public class BooksReservationConfiguration : IEntityTypeConfiguration<BooksReservation>
{
	public void Configure(EntityTypeBuilder<BooksReservation> builder)
	{
		builder.ToTable("BooksReservations");

		builder.HasKey(br => br.Id);

		builder.Property(br => br.TenantId)
			   .IsRequired(false);

		builder.Property(br => br.BookId)
			   .IsRequired();

		builder.Property(br => br.MemberId)
			   .IsRequired();

		builder.Property(br => br.RequestedDate)
			   .IsRequired();

		builder.Property(br => br.FulfilledDate)
			   .IsRequired(false);

		builder.Property(br => br.Status)
			   .IsRequired();

		// Define Foreign Keys
		builder.HasOne<Book>()
			   .WithMany()
			   .HasForeignKey(br => br.BookId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne<Member>()
			   .WithMany()
			   .HasForeignKey(br => br.MemberId)
			   .OnDelete(DeleteBehavior.Restrict);
	}
}
