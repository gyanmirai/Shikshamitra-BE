using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadConfiguration : IEntityTypeConfiguration<FeeHead>
{
	public void Configure(EntityTypeBuilder<FeeHead> builder)
	{
		builder.ToTable("Finance_FeeHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// add navigation for fee category
		builder.HasOne(fh => fh.FeeCategory)
			.WithMany()
			.HasForeignKey(fh => fh.FeeCategoryId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.Navigation(fh => fh.FeeCategory)
			.AutoInclude();
	}
}
