using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.BookManagement.ReturnBooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.BookManagement
{
	public class ReturnBookConfiguration : IEntityTypeConfiguration<ReturnBook>
	{
		public void Configure(EntityTypeBuilder<ReturnBook> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ReturnBooks", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention(); // Auto-configure auditing and multi-tenancy

			builder.Property(b => b.TenantId)
				   .IsRequired(false);

			builder.Property(b => b.BookId)
				   .IsRequired();

			builder.Property(b => b.BookTitle)
				   .IsRequired()
				   .HasMaxLength(128);

			builder.Property(b => b.MemberId)
				   .IsRequired();

			builder.Property(b => b.MemberName)
				   .IsRequired()
				   .HasMaxLength(128);

			builder.Property(b => b.DueDate)
				   .IsRequired();

			builder.Property(b => b.ReturnDate)
				   .IsRequired();

			builder.Property(b => b.Condition)
				   .HasMaxLength(64);

			builder.Property(b => b.FineAmount)
				   .HasColumnType("decimal(18,2)")
				   .IsRequired();

			builder.Property(b => b.FineReason)
				   .HasMaxLength(256);

			builder.Property(b => b.ReceivedFineAmount)
				   .HasColumnType("decimal(18,2)")
				   .IsRequired();

			builder.Property(b => b.PaymentMode)
				   .HasMaxLength(32);

			builder.Property(b => b.NotificationSentOn)
				   .IsRequired(false);

			builder.Property(b => b.NotificationStatus)
				   .HasMaxLength(64);

			builder.Property(b => b.Action)
				   .HasMaxLength(64);
		}
	}
}
