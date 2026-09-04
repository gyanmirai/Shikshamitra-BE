using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public class ConcessionTypeDocumentFormatConfiguration : IEntityTypeConfiguration<ConcessionTypeDocumentFormat>
{
	public void Configure(EntityTypeBuilder<ConcessionTypeDocumentFormat> builder)
	{
		builder.ToTable("Finance_ConcessionTypeDocumentFormats", ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();
		builder.HasIndex(x => new { x.ConcessionTypeDocumentId, x.DocumentType })
			.IsUnique()
			.HasDatabaseName("UX_Finance_ConcessionTypeDocumentFormats_Document_Format");
	}
}
