using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportVehicleConfiguration : IEntityTypeConfiguration<TransportVehicle>
{
	public void Configure(EntityTypeBuilder<TransportVehicle> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportVehicles", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.RegistrationNo).IsRequired().HasMaxLength(30);
		builder.Property(x => x.VehicleNo).IsRequired().HasMaxLength(30);
		builder.Property(x => x.VehicleName).HasMaxLength(100);
		builder.Property(x => x.VehicleTypeId).IsRequired();
		builder.Property(x => x.VehicleCompany).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Model).IsRequired().HasMaxLength(100);
		builder.Property(x => x.YearOfManufacture).IsRequired().HasMaxLength(4);
		builder.Property(x => x.SeatingCapacity).IsRequired();
		builder.Property(x => x.VehicleColor).IsRequired().HasMaxLength(50);
		builder.Property(x => x.SchoolAssignedNo).HasMaxLength(50);
		builder.Property(x => x.Owner).IsRequired().HasMaxLength(100);
		builder.Property(x => x.FuelTypeId).IsRequired();
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Status).IsRequired();

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.RegistrationNo }).IsUnique();
		builder.HasIndex(x => x.VehicleTypeId);
		builder.HasIndex(x => x.FuelTypeId);
	}
}
