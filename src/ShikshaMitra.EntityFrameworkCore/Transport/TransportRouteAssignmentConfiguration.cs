using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportRouteAssignmentConfiguration : IEntityTypeConfiguration<TransportRouteAssignment>
{
	public void Configure(EntityTypeBuilder<TransportRouteAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportRouteAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.RouteId).IsRequired();
		builder.Property(x => x.VehicleId).IsRequired();
		builder.Property(x => x.DriverStaffId).IsRequired();
		builder.Property(x => x.ConductorStaffId);
		builder.Property(x => x.EffectiveFrom).IsRequired();
		builder.Property(x => x.EffectiveTo);
		builder.Property(x => x.Remarks).HasMaxLength(500);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => x.RouteId);
		builder.HasIndex(x => x.VehicleId);
		builder.HasIndex(x => x.DriverStaffId);
		builder.HasIndex(x => x.ConductorStaffId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.RouteId, x.EffectiveTo });
		builder.HasIndex(x => new { x.AcademicMediumId, x.VehicleId, x.EffectiveTo });
		builder.HasIndex(x => new { x.AcademicMediumId, x.DriverStaffId, x.EffectiveTo });
		builder.HasIndex(x => new { x.AcademicMediumId, x.ConductorStaffId, x.EffectiveTo });
	}
}
