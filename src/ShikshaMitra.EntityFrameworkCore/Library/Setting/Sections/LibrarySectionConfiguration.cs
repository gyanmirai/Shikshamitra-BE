using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.Sections;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Setting.Sections
{
	public class LibrarySectionConfiguration : IEntityTypeConfiguration<LibrarySection>
	{
		public void Configure(EntityTypeBuilder<LibrarySection> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibrarySections", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.SectionName)
				.IsRequired()
				.HasMaxLength(128);

			builder.Property(x => x.NumberOfShelf)
				.IsRequired();

			builder.Property(x => x.AcademicYearId);

			builder.HasMany(x => x.Shelves)
				.WithOne()
				.HasForeignKey(x => x.SectionId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}

	public class LibraryShelfConfiguration : IEntityTypeConfiguration<LibraryShelf>
	{
		public void Configure(EntityTypeBuilder<LibraryShelf> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryShelves", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.ShelfName)
				.IsRequired()
				.HasMaxLength(128);

			builder.Property(x => x.SectionId)
				.IsRequired();
		}
	}
}
