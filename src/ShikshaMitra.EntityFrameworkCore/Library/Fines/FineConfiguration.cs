using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.BooksAssignments;
using ShikshaMitra.Library.Members;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Fines;

public class FineConfiguration : IEntityTypeConfiguration<Fine>
{
	public void Configure(EntityTypeBuilder<Fine> builder)
	{
		builder.ToTable("Fines");

		builder.HasKey(f => f.Id);

		builder.Property(f => f.TenantId)
			   .IsRequired(false);

		builder.Property(f => f.MemberId)
			   .IsRequired();

		builder.Property(f => f.BookAssignmentId)
			   .IsRequired();

		builder.Property(f => f.TotalFineAmount)
			   .IsRequired()
			   .HasColumnType("decimal(18,2)");

		builder.Property(f => f.Status)
			   .IsRequired()
			   .HasMaxLength(50);

		builder.Property(f => f.PaymentMode)
			   .IsRequired();

		builder.HasOne<Member>()
			   .WithMany()
			   .HasForeignKey(f => f.MemberId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne<BooksAssignment>()
			   .WithMany()
			   .HasForeignKey(f => f.BookAssignmentId)
			   .OnDelete(DeleteBehavior.Restrict);
	}
}
