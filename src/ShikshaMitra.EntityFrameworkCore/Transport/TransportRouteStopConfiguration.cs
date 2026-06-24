using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportRouteStopConfiguration : IEntityTypeConfiguration<TransportRouteStop>
{
	public void Configure(EntityTypeBuilder<TransportRouteStop> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportRouteStops", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.RouteId).IsRequired();
		builder.Property(x => x.StopId).IsRequired();
		builder.Property(x => x.StopSequence).IsRequired();
		builder.Property(x => x.DistanceFromPrevious).HasPrecision(18, 2);

		builder.HasOne<TransportRoute>()
			.WithMany()
			.HasForeignKey(x => x.RouteId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne<TransportStop>()
			.WithMany()
			.HasForeignKey(x => x.StopId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.RouteId);
		builder.HasIndex(x => x.StopId);
		builder.HasIndex(x => new { x.RouteId, x.StopId }).IsUnique();
		builder.HasIndex(x => new { x.RouteId, x.StopSequence }).IsUnique();
	}
}
