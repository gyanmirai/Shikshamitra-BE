using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportServiceTypeConfiguration : IEntityTypeConfiguration<TransportServiceType>
{
	public void Configure(EntityTypeBuilder<TransportServiceType> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportServiceTypes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.IsActive).IsRequired();

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.Name }).IsUnique();
	}
}
