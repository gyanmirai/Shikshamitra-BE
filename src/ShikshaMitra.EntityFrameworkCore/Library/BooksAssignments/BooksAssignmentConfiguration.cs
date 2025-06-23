using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;

namespace ShikshaMitra.Library.BooksAssignments;

public class BooksAssignmentConfiguration : IEntityTypeConfiguration<BooksAssignment>
{
	public void Configure(EntityTypeBuilder<BooksAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BooksAssignments", ShikshaMitraConsts.DbSchema);

		builder.HasKey(ba => ba.Id);

		builder.Property(ba => ba.TenantId)
			.IsRequired(false);

		builder.Property(ba => ba.BookId)
			.IsRequired();

		builder.Property(ba => ba.MemberId)
			.IsRequired();

		builder.Property(ba => ba.BookTitle)
			.IsRequired();

		builder.Property(ba => ba.IssueDate)
			.IsRequired();

		builder.Property(ba => ba.DueDate)
			.IsRequired();

		builder.Property(ba => ba.ReturnDate)
			.IsRequired(false);

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
