using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.BookManagement.BookOverviews;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.BookManagement
{
	public class BookOverviewConfiguration : IEntityTypeConfiguration<BookOverview>
	{
		public void Configure(EntityTypeBuilder<BookOverview> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BookOverviews", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention(); // Auto-configure common auditing and extra properties

			builder.Property(x => x.TenantId)
				   .IsRequired(false);

			builder.Property(x => x.ISBN)
				   .IsRequired()
				   .HasMaxLength(50);

			builder.Property(x => x.Name)
				   .IsRequired()
				   .HasMaxLength(128);

			builder.Property(x => x.Borrowed)
				   .IsRequired();

			builder.Property(x => x.Overdue)
				   .IsRequired();

			builder.Property(x => x.Reserved)
				   .IsRequired();

			builder.HasIndex(x => x.ISBN);
			builder.HasIndex(x => x.TenantId);
		}
    }
}
