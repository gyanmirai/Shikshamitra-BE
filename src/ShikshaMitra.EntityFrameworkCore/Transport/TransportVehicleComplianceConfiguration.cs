using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportVehicleComplianceConfiguration : IEntityTypeConfiguration<TransportVehicleCompliance>
{
	public void Configure(EntityTypeBuilder<TransportVehicleCompliance> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportVehicleCompliances", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.VehicleId).IsRequired();
		builder.Property(x => x.InsuranceCompany).HasMaxLength(100);
		builder.Property(x => x.PolicyNo).HasMaxLength(50);
		builder.Property(x => x.CoverageAmount).HasPrecision(18, 2);
		builder.Property(x => x.PucCertificateNo).HasMaxLength(50);
		builder.Property(x => x.FitnessCertificateNo).HasMaxLength(50);
		builder.Property(x => x.RoadTaxReceiptNo).HasMaxLength(50);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => x.VehicleId).IsUnique();
	}
}
