using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Buildings;

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
	public void Configure(EntityTypeBuilder<Floor> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Floors", ShikshaMitraConsts.DbSchema);

		builder.HasKey(x => x.Id);

		builder.Property(x => x.FloorName)
			.IsRequired()
			.HasMaxLength(FloorConsts.MaxFloorNameLength);

		builder.Property(x => x.FloorNo)
			.IsRequired();

		builder.Property(x => x.TotalRoom)
			.IsRequired();

		builder.HasOne(x => x.Building)
			.WithMany(x => x.Floors)
			.HasForeignKey(x => x.BuildingId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
