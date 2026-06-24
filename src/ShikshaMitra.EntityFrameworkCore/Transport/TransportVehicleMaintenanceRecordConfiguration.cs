using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportVehicleMaintenanceRecordConfiguration : IEntityTypeConfiguration<TransportVehicleMaintenanceRecord>
{
	public void Configure(EntityTypeBuilder<TransportVehicleMaintenanceRecord> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportVehicleMaintenanceRecords", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.VehicleId).IsRequired();
		builder.Property(x => x.ServiceType).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Cost).HasPrecision(18, 2);
		builder.Property(x => x.Provider).HasMaxLength(100);
		builder.Property(x => x.Notes).HasMaxLength(1000);
		builder.Property(x => x.PhotoBlobNames).HasMaxLength(2000);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => x.VehicleId);
		builder.HasIndex(x => x.ServiceDate);
	}
}
