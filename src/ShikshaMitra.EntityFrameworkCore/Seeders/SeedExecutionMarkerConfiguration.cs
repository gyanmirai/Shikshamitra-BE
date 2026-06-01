using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Seeders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Seeders;

public class SeedExecutionMarkerConfiguration : IEntityTypeConfiguration<SeedExecutionMarker>
{
	public void Configure(EntityTypeBuilder<SeedExecutionMarker> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "SeedExecutionMarkers", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(x => x.Id);
		builder.Property(x => x.SeederName).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Version).IsRequired().HasMaxLength(64);
		builder.Property(x => x.Profile).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.Error).HasMaxLength(4000);

		builder.HasIndex(x => new { x.TenantId, x.SeederName, x.Version, x.Profile })
			.IsUnique()
			.HasDatabaseName("IX_SeedExecutionMarkers_Tenant_Seeder_Version_Profile");
	}
}
