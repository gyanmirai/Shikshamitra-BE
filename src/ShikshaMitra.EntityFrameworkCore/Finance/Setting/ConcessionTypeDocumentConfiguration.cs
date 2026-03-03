using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using ShikshaMitra.Finance.Setting.ConcessionType;

namespace ShikshaMitra.Finance.Setting;

public class ConcessionTypeDocumentConfiguration : IEntityTypeConfiguration<ConcessionTypeDocument>
{
	public void Configure(EntityTypeBuilder<ConcessionTypeDocument> builder)
	{
		builder.ToTable("ConcessionTypeDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	}
}
