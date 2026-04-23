using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings;

public class ChargeCategoryConfiguration : IEntityTypeConfiguration<ChargeCategory>
{
	public void Configure(EntityTypeBuilder<ChargeCategory> builder)
	{
		builder.ToTable("ChargeCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
