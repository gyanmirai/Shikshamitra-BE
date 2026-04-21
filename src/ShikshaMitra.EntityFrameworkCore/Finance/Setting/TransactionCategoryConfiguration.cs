using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting;

namespace ShikshaMitra.Finance.Setting;

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
