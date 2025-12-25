using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Records.Settings;

public class HouseSettingsConfiguration : IEntityTypeConfiguration<HouseSettings>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<HouseSettings> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "HouseSettings", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(200);
		builder.Property(x => x.Color)
			.IsRequired()
			.HasMaxLength(50);
		builder.Property(x => x.LogoUrl)
			.HasMaxLength(500);
		builder.Property(x => x.Description)
			.HasMaxLength(1000);
		// Index on AcademicMediumId for faster lookups
		builder.HasIndex(x => x.AcademicMediumId);
	}
}
