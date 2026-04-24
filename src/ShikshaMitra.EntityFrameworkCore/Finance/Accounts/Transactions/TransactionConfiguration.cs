using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Transactions;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
	public void Configure(EntityTypeBuilder<Transaction> builder)
	{
		builder.ToTable("Transactions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Notes).HasMaxLength(2000);
		builder.Property(x => x.Amount).HasColumnType("numeric(18,4)");

		builder.OwnsMany(x => x.CategoryTags, tag =>
		{
			tag.WithOwner().HasForeignKey("TransactionId");
			tag.Property(t => t.Value)
				.HasColumnName("Value")
				.HasMaxLength(Tag.MaxLength)
				.IsRequired();
			tag.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransactionCategoryTags", ShikshaMitraConsts.DbSchema);
		});
	}
}
