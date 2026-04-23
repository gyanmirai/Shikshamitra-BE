using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public class ConcessionTypeDocumentFormatConfiguration : IEntityTypeConfiguration<ConcessionTypeDocumentFormat>
{
	public void Configure(EntityTypeBuilder<ConcessionTypeDocumentFormat> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ConcessionTypeDocumentFormats", ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();
	}
}
