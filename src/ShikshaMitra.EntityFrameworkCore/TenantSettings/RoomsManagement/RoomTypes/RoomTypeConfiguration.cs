using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;

public class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
	public void Configure(EntityTypeBuilder<RoomType> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "RoomTypes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.RoomTypeName)
			.IsRequired()
			.HasMaxLength(RoomTypeConsts.MaxRoomTypeLength);

		builder.Property(x => x.ShortName)
			.IsRequired()
			.HasMaxLength(RoomTypeConsts.MaxShortNameLength);
	}
}
