using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Setting.General_Setting
{
	public class BookCategoryConfiguration : IEntityTypeConfiguration<BookCategory>
	{
		public void Configure(EntityTypeBuilder<BookCategory> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BookCategories", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention();

            builder.Property(x => x.Name)
				.IsRequired()
				.HasMaxLength(128);

			builder.Property(x => x.Description)
				.IsRequired();
		}
	}
}
