using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Setting;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
	public void Configure(EntityTypeBuilder<BankAccount> builder)
	{
		builder.ToTable("BankAccounts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
