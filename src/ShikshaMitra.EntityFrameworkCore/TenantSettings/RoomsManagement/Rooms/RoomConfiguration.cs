using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

		// Configure Features as JSONB with proper value conversion
		var stringListToJsonConverter = new ValueConverter<List<string>?, string>(
			v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
			v => JsonSerializer.Deserialize<List<string>?>(v, (JsonSerializerOptions?)null)
		);

		var stringListValueComparer = new ValueComparer<List<string>?>(
			(c1, c2) => c1 != null && c2 != null ? c1.SequenceEqual(c2) : c1 == c2,
			c => c != null ? c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)) : 0,
			c => c != null ? c.ToList() : null
		);

		builder.Property(x => x.Features)
			.HasColumnType("jsonb")
			.HasConversion(stringListToJsonConverter)
			.Metadata.SetValueComparer(stringListValueComparer);

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