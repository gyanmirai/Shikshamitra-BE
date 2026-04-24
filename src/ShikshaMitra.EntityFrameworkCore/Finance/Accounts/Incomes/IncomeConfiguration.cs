using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Incomes;

public class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
	public void Configure(EntityTypeBuilder<Income> builder)
	{
		builder.ToTable("Incomes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).IsRequired().HasMaxLength(500);
		builder.Property(x => x.CategoryTags).HasMaxLength(2000);
		builder.Property(x => x.Notes).HasMaxLength(2000);
		builder.Property(x => x.Amount).HasColumnType("numeric(18,4)");
	}
}
