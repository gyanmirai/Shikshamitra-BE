using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

public class FeeStructureDetailConfiguration : IEntityTypeConfiguration<FeeStructureDetail>
{
	public void Configure(EntityTypeBuilder<FeeStructureDetail> builder)
	{
		builder.ToTable("FeeStructureDetails", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
