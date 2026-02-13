using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Setting;

public class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
	public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
	{
		builder.ToTable("ExpenseCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
