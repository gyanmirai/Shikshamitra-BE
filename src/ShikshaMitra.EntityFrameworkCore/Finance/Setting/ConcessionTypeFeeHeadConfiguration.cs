using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting.ConcessionType;

namespace ShikshaMitra.Finance.Setting;

public class ConcessionTypeFeeHeadConfiguration : IEntityTypeConfiguration<ConcessionTypeFeeHead>
{
	public void Configure(EntityTypeBuilder<ConcessionTypeFeeHead> builder)
	{
		builder.ToTable("ConcessionTypeFeeHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
