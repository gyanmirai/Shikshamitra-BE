using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.BooksAssignements;
using ShikshaMitra.Library.Members;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BooksAssignments
{
	public class BooksAssignmentConfiguration : IEntityTypeConfiguration<BooksAssignment>
	{
		public void Configure(EntityTypeBuilder<BooksAssignment> builder)
		{
			builder.ToTable("BooksAssignments");

			builder.HasKey(ba => ba.Id);

			builder.Property(ba => ba.TenantId)
				.IsRequired(false);

			builder.Property(ba => ba.BookId)
				.IsRequired();

			builder.Property(ba => ba.MemberId)
				.IsRequired();

			builder.Property(ba => ba.IssueDate)
				.IsRequired();

			builder.Property(ba => ba.DueDate)
				.IsRequired();

			builder.Property(ba => ba.ReturnDate)
				.IsRequired(false);

			builder.Property(ba => ba.FineAmount)
				.HasColumnType("decimal(18,2)")
				.IsRequired();

			builder.Property(ba => ba.Status)
				.IsRequired()
				.HasMaxLength(50);

			builder.HasOne<Book>()
				.WithMany()
				.HasForeignKey(ba => ba.BookId)
				.OnDelete(DeleteBehavior.Restrict);

			builder.HasOne<Member>()
				.WithMany()
				.HasForeignKey(ba => ba.MemberId)
				.OnDelete(DeleteBehavior.Restrict);
		}
	}
}
