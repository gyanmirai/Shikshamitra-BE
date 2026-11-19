using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.InitialSetup;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.TenantSettings;

internal class InstituteSetupProgressConfiguration : IEntityTypeConfiguration<InstituteSetupProgress>
{
	public void Configure(EntityTypeBuilder<InstituteSetupProgress> builder)
	{
		builder.ToTable("AppInstituteSetupProgress", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.TenantId).IsRequired();
		builder.HasIndex(x => x.TenantId).IsUnique();
		builder.Property(x => x.DraftJson).IsRequired().HasMaxLength(16384);
	}
}
