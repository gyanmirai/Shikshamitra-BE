using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;

public class PaymentStatusOptionConfiguration
{
	public void Configure(EntityTypeBuilder<PaymentStatusOption> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PaymentStatusOptions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
