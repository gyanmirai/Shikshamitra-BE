using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Setting.FeeStructure;
using Volo.Abp.EntityFrameworkCore.Modeling;


public class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
{
	public void Configure(EntityTypeBuilder<FeeStructure> builder)
	{
		builder.ToTable("FeeStructures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
