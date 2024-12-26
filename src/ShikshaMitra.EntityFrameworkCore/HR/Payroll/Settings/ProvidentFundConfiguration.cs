using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProvidentFundConfiguration : IEntityTypeConfiguration<ProvidentFund>
{
	public void Configure(EntityTypeBuilder<ProvidentFund> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ProvidentFunds", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		
	}
}