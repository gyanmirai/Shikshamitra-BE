using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Setting;

public class ReceiptBookConfiguration : IEntityTypeConfiguration<ReceiptBook>
{
	public void Configure(EntityTypeBuilder<ReceiptBook> builder)
	{
		builder.ToTable("ReceiptBooks", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
	}
}
