using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.ChargeCategory;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Setting.ChargeCategory
{
	public class LibraryChargeCategoryConfiguration : IEntityTypeConfiguration<LibraryChargeCategory>
	{
		public void Configure(EntityTypeBuilder<LibraryChargeCategory> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryChargeCategories", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.ChargeType)
				.IsRequired()
				.HasMaxLength(128);

			builder.Property(x => x.Amount)
				.IsRequired()
				.HasColumnType("numeric(18,2)");

			builder.Property(x => x.IsFixed);

			builder.Property(x => x.Description)
				.HasMaxLength(512);

			builder.Property(x => x.AcademicYearId)
				.IsRequired();
		}
	}
}
