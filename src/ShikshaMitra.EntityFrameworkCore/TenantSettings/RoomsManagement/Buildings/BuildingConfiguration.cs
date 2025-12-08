using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
	public void Configure(EntityTypeBuilder<Building> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Buildings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Id)
			.ValueGeneratedOnAdd();

		builder.Property(x => x.BuildingName)
			.IsRequired()
			.HasMaxLength(BuildingConsts.MaxNameLength);

		builder.Property(x => x.TotalFloors)
			.IsRequired();

		builder.Property(x => x.Description)
			.HasMaxLength(BuildingConsts.MaxDescriptionLength);

		builder.HasMany(x => x.Floors)
			.WithOne(x => x.Building)
			.HasForeignKey(x => x.BuildingId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
