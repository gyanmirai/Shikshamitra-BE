using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagementConfiguration : IEntityTypeConfiguration<ShiftManagement>
{
	public void Configure(EntityTypeBuilder<ShiftManagement> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ShiftManagements", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
