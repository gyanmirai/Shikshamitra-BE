using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.TenantSettings;

internal class InstituteSetupExtraPropertiesConfiguration : IEntityTypeConfiguration<InstituteSetupExtraProperties>
{
	public void Configure(EntityTypeBuilder<InstituteSetupExtraProperties> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "InstituteSetupExtraProperties", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
