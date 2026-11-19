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
		builder.ToTable("Finance_Transactions", ShikshaMitraConsts.DbSchema);
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount>().WithMany().HasForeignKey(x=>x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
		builder.ConfigureByConvention();
		builder.HasOne<ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeeAccount>().WithMany()
			.HasForeignKey(x => x.FinanceFeeAccountId).OnDelete(DeleteBehavior.Restrict);

		builder.Ignore(x => x.IsAutomatic);
		builder.HasIndex(x => x.CashCollectionId);
		builder.HasOne<ShikshaMitra.Finance.Accounts.CashManagement.CashCollection>().WithMany()
			.HasForeignKey(x => x.CashCollectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x=>x.SourceId);
  builder.Property(x => x.SourceKey).HasMaxLength(200);
  builder.Property(x => x.CategoryNameSnapshot).HasMaxLength(500);
  builder.Property(x => x.BankLabelSnapshot).HasMaxLength(500);
  builder.Property(x => x.IsConfirmed).HasDefaultValue(true);
  builder.HasIndex(x => x.SourceKey).IsUnique().HasFilter("\"SourceKey\" IS NOT NULL");
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
