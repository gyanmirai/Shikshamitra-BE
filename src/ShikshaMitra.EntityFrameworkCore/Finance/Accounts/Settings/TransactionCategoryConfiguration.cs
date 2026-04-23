using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class TransactionCategoryConfiguration : IEntityTypeConfiguration<TransactionCategory>
{
	public void Configure(EntityTypeBuilder<TransactionCategory> builder)
	{
		builder.ToTable("TransactionCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.OwnsMany(x => x.Tags, tag =>
		{
			tag.WithOwner().HasForeignKey("TransactionCategoryId");
			tag.Property(t => t.Value)
				.HasColumnName("Value")
				.HasMaxLength(Tag.MaxLength)
				.IsRequired();
			tag.ToTable(ShikshaMitraConsts.DbTablePrefix + "IncomeCategoryTags", ShikshaMitraConsts.DbSchema);
		});
	}
}
