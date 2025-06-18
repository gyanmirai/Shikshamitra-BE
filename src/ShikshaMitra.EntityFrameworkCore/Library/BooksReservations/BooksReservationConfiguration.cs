using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.BooksAssignments;
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
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BooksReservations", ShikshaMitraConsts.DbSchema);

		builder.HasKey(br => br.Id);

		builder.Property(br => br.TenantId)
			   .IsRequired(false);

		builder.Property(br => br.BooksAssignmentId)
			   .IsRequired();

		builder.Property(br => br.RequestedDate)
			   .IsRequired();

		builder.Property(br => br.FulfilledDate)
			   .IsRequired(false);

		builder.Property(br => br.Status)
			   .IsRequired();

		builder.HasOne<BooksAssignment>()
				.WithMany()
				.HasForeignKey(br => br.BooksAssignmentId)
				.OnDelete(DeleteBehavior.Restrict);
	}
}
