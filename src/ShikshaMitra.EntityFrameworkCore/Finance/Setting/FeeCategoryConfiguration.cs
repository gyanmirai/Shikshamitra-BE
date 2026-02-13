using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Setting;

public class FeeCategoryConfiguration : IEntityTypeConfiguration<FeeCategory>
{
	public void Configure(EntityTypeBuilder<FeeCategory> builder)
	{
		builder.ToTable("FeeCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
