using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadConfiguration : IEntityTypeConfiguration<FeeHead>
{
	public void Configure(EntityTypeBuilder<FeeHead> builder)
	{
		builder.ToTable("FeeHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
