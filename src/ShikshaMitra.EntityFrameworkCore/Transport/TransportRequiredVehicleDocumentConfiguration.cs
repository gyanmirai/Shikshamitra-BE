using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportRequiredVehicleDocumentConfiguration : IEntityTypeConfiguration<TransportRequiredVehicleDocument>
{
	public void Configure(EntityTypeBuilder<TransportRequiredVehicleDocument> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportRequiredVehicleDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.AcceptedFormatsCsv).IsRequired().HasMaxLength(100);
		builder.Property(x => x.MaxSizeMb).IsRequired();
		builder.Property(x => x.IsRequired).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.Name }).IsUnique();
	}
}
