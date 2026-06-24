using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportVehicleDocumentConfiguration : IEntityTypeConfiguration<TransportVehicleDocument>
{
	public void Configure(EntityTypeBuilder<TransportVehicleDocument> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportVehicleDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.VehicleId).IsRequired();
		builder.Property(x => x.RequiredDocumentId).IsRequired();
		builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
		builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
		builder.Property(x => x.BlobName).IsRequired().HasMaxLength(512);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => x.VehicleId);
		builder.HasIndex(x => new { x.VehicleId, x.RequiredDocumentId }).IsUnique();
	}
}
