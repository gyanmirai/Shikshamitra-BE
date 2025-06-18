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
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Fines", ShikshaMitraConsts.DbSchema);

		builder.HasKey(f => f.Id);

		builder.Property(f => f.TenantId)
			   .IsRequired(false);

        builder.Property(f => f.BooksAssignmentId)
			   .IsRequired();

		builder.Property(f => f.FineAmount)
			   .IsRequired()
			   .HasColumnType("decimal(18,2)");

		builder.Property(f => f.PaymentMode)
			   .IsRequired();

		builder.Property(f => f.PenaltyAmount)
				.IsRequired()
				.HasColumnType("decimal(18,2)");

		builder.Property(f => f.PenaltyReason)
				   .HasMaxLength(256);

		builder.Property(f => f.BankAccountNumber)
				   .HasMaxLength(50);

		builder.Property(f => f.IFSCCode)
				   .HasMaxLength(20);

		builder.Property(f => f.AccountHolderName)
				   .HasMaxLength(100);

        builder.HasOne<BooksAssignment>()
			   .WithMany()
			   .HasForeignKey(f => f.BooksAssignmentId)
			   .OnDelete(DeleteBehavior.Restrict);
	}
}
