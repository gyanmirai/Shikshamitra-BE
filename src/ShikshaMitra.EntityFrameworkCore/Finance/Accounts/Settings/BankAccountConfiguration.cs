using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
	public void Configure(EntityTypeBuilder<BankAccount> builder)
	{
		builder.ToTable("BankAccounts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
