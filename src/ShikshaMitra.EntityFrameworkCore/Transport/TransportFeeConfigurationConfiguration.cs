using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportFeeConfigurationConfiguration : IEntityTypeConfiguration<TransportFeeConfiguration>
{
	public void Configure(EntityTypeBuilder<TransportFeeConfiguration> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportFeeConfigurations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.ModelType).IsRequired();
		builder.Property(x => x.FeeFrequency).IsRequired();
		builder.Property(x => x.RatePerKm).HasPrecision(18, 2);
		builder.Property(x => x.ChargeCategoryId).IsRequired();
		builder.Property(x => x.EffectiveFrom).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();
		builder.Property(x => x.Remarks).HasMaxLength(500);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.IsActive });
		builder.HasIndex(x => new { x.AcademicMediumId, x.ModelType, x.IsActive });
		builder.HasIndex(x => x.ChargeCategoryId);
	}
}
