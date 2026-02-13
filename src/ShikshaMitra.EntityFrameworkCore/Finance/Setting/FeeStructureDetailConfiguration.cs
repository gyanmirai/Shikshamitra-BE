using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting.FeeStructure;

namespace ShikshaMitra.Finance.Setting;

public class FeeStructureDetailConfiguration : IEntityTypeConfiguration<FeeStructureDetail>
{
	public void Configure(EntityTypeBuilder<FeeStructureDetail> builder)
	{
		builder.ToTable("FeeStructureDetails", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
