using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;



	

public class FeeHeadConfiguration
{
	public void Configure(EntityTypeBuilder<FeeHead> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "FeeHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}


   }

