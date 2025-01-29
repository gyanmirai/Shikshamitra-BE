using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;



	

public class FeeHeadConfiguration
{
	public void Configure(EntityTypeBuilder<FeeHead> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "FeeHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}


   }

