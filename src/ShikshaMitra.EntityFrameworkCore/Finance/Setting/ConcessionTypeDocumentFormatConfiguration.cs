using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Setting.ConcessionType;
using ShikshaMitra.Finance.Setting.ConcessionTypes;

namespace ShikshaMitra.Finance.Setting;

public class ConcessionTypeDocumentFormatConfiguration : IEntityTypeConfiguration<ConcessionTypeDocumentFormat>
{
	public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ConcessionTypeDocumentFormat> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ConcessionTypeDocumentFormats", ShikshaMitraConsts.DbSchema);

		builder.HasKey(e => e.ConcessionTypeDocumentId);
		builder.Property(e => e.ConcessionTypeDocumentId).ValueGeneratedNever();
		builder.Property(e => e.DocumentType).IsRequired();
	}
}
