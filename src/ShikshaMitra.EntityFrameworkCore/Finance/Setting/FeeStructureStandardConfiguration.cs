using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting.FeeStructure;

namespace ShikshaMitra.Finance.Setting;

public class FeeStructureStandardConfiguration : IEntityTypeConfiguration<FeeStructureStandard>
{
	public void Configure(EntityTypeBuilder<FeeStructureStandard> builder)
	{
		builder.ToTable("FeeStructureStandards", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
