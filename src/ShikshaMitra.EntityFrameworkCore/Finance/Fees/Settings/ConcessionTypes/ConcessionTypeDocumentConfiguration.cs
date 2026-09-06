using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public class ConcessionTypeDocumentConfiguration : IEntityTypeConfiguration<ConcessionTypeDocument>
{
	public void Configure(EntityTypeBuilder<ConcessionTypeDocument> builder)
	{
		builder.ToTable("Finance_ConcessionTypeDocuments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.DocumentName).HasMaxLength(ConcessionTypeConsts.MaxDocumentNameLength).IsRequired();
		builder.Property(x => x.MaxFileSizeBytes).IsRequired();
		builder.Property(x => x.IsRequired).IsRequired().HasDefaultValue(true);

		builder.HasOne<ConcessionType>()
			.WithMany(x => x.ConcessionTypeDocuments)
			.HasForeignKey(x => x.ConcessionTypeId)
			.OnDelete(DeleteBehavior.Cascade);

		// Configure the relationship between ConcessionTypeDocument and ConcessionTypeDocumentFormat
		builder.HasMany(x => x.AcceptedFormats)
			.WithOne()
			.HasForeignKey(x => x.ConcessionTypeDocumentId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(x => x.AcceptedFormats).AutoInclude();
	}
}
