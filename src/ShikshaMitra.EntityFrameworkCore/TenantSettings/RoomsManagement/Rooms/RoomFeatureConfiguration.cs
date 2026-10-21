using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomFeatureConfiguration : IEntityTypeConfiguration<RoomFeature>
{
	public void Configure(EntityTypeBuilder<RoomFeature> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "RoomFeatures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(100);
		builder.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique()
			.AreNullsDistinct(false);
	}
}
