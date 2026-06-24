using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportStopConfiguration : IEntityTypeConfiguration<TransportStop>
{
	public void Configure(EntityTypeBuilder<TransportStop> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportStops", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.StopName).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Landmark).IsRequired().HasMaxLength(100);
		builder.Property(x => x.ZoneId).HasMaxLength(100);
		builder.Property(x => x.AddressLine1).HasMaxLength(500);
		builder.Property(x => x.AddressLine2).HasMaxLength(500);
		builder.Property(x => x.PinCode).HasMaxLength(20);
		builder.Property(x => x.Amount).HasPrecision(18, 2);
		builder.Property(x => x.ApproxKmFromSchool).HasPrecision(18, 2);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.Status).IsRequired();

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.StopName });
	}
}
