using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.BookManagement.IssueBooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.BookManagement;

public class IssueBookConfiguration : IEntityTypeConfiguration<IssueBook>
{
	public void Configure(EntityTypeBuilder<IssueBook> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "IssueBooks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention(); // Auto-configure common auditing and extra properties

		// Same sequence as the IssueBook entity class
		builder.Property(b => b.TenantId)
			   .IsRequired(false);

		builder.Property(b => b.Standard)
			   .IsRequired()
			   .HasMaxLength(64);

		builder.Property(b => b.Division)
			   .IsRequired()
			   .HasMaxLength(64);

		builder.Property(b => b.GrNumber)
			   .IsRequired()
			   .HasMaxLength(32);

		builder.Property(b => b.StudentName)
			   .IsRequired()
			   .HasMaxLength(128);

		builder.Property(b => b.BookId)
			   .IsRequired();

		builder.Property(b => b.BookTitle)
			   .IsRequired()
			   .HasMaxLength(128);

		builder.Property(b => b.EditionYear)
			   .IsRequired();

		builder.Property(b => b.Publisher)
			   .HasMaxLength(128);

		builder.Property(b => b.IssueDate)
			   .IsRequired();

		builder.Property(b => b.DueDate)
			   .IsRequired();

		builder.Property(b => b.HasPendingCharges)
			   .IsRequired();
	}
}
