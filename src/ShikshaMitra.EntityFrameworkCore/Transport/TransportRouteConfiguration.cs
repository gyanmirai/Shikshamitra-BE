using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportRouteConfiguration : IEntityTypeConfiguration<TransportRoute>
{
	public void Configure(EntityTypeBuilder<TransportRoute> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportRoutes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.RouteName).IsRequired().HasMaxLength(100);
		builder.Property(x => x.RouteCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.RouteType).IsRequired().HasMaxLength(20);
		builder.Property(x => x.Distance).HasPrecision(18, 2);
		builder.Property(x => x.Duration).HasMaxLength(50);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.StartPoint).IsRequired().HasMaxLength(100);
		builder.Property(x => x.EndPoint).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Status).IsRequired();

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.RouteCode }).IsUnique();
	}
}
