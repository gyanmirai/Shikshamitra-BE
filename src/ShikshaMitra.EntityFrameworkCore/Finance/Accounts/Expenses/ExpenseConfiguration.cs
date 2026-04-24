using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Expenses;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
	public void Configure(EntityTypeBuilder<Expense> builder)
	{
		builder.ToTable("Expenses", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).IsRequired().HasMaxLength(500);
		builder.Property(x => x.CategoryTags).HasMaxLength(2000);
		builder.Property(x => x.Notes).HasMaxLength(2000);
		builder.Property(x => x.Amount).HasColumnType("numeric(18,4)");
	}
}
