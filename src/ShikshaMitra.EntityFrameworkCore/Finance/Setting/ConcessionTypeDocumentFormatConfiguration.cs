using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Setting.ConcessionTypes;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Setting;

public class ConcessionTypeDocumentFormatConfiguration : IEntityTypeConfiguration<ConcessionTypeDocumentFormat>
{
	public void Configure(EntityTypeBuilder<ConcessionTypeDocumentFormat> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ConcessionTypeDocumentFormats", ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();
	}
}
