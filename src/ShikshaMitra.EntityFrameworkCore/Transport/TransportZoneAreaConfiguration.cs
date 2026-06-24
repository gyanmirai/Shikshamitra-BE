using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportZoneAreaConfiguration : IEntityTypeConfiguration<TransportZoneArea>
{
	public void Configure(EntityTypeBuilder<TransportZoneArea> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportZoneAreas", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.IsActive).IsRequired();

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.Name });
		builder.HasIndex(x => new { x.AcademicMediumId, x.IsActive });
	}
}
