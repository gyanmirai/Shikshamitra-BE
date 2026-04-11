using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Setting;

public class ChargeCategoryConfiguration : IEntityTypeConfiguration<ChargeCategory>
{
	public void Configure(EntityTypeBuilder<ChargeCategory> builder)
	{
		builder.ToTable("ChargeCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
