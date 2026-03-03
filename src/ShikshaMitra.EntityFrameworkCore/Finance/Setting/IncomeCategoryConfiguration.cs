using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Setting;

public class IncomeCategoryConfiguration : IEntityTypeConfiguration<IncomeCategory>
{
	public void Configure(EntityTypeBuilder<IncomeCategory> builder)
	{
		builder.ToTable("IncomeCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
