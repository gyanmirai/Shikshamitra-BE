using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
	public void Configure(EntityTypeBuilder<Room> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Rooms", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.RoomName)
			.IsRequired()
			.HasMaxLength(RoomConsts.MaxRoomNameLength);

		builder.Property(x => x.Capacity)
			.IsRequired();

		builder.Property(x => x.BuildingId)
			.IsRequired();

		builder.Property(x => x.Floor)
			.IsRequired();

		builder.Property(x => x.RoomTypeId)
			.IsRequired();

		builder.Property(x => x.Features)
			.HasMaxLength(RoomConsts.MaxFeaturesLength);

		// Foreign Key Relationships
		builder.HasOne<Building>()
			.WithMany()
			.HasForeignKey(r => r.BuildingId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne<RoomType>()
			.WithMany()
			.HasForeignKey(r => r.RoomTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		// Indexes for common queries
		builder.HasIndex(x => x.BuildingId);
		builder.HasIndex(x => x.RoomTypeId);
	}
}
