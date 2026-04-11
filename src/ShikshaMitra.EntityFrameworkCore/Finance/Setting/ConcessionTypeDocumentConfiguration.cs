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

		// Configure the relationship between ConcessionTypeDocument and ConcessionTypeDocumentFormat
		builder.HasMany(x => x.AcceptedFormats)
			.WithOne()
			.HasForeignKey("ConcessionTypeDocumentId") // Assuming the foreign key is named "ConcessionTypeDocumentId"
			.OnDelete(DeleteBehavior.Cascade); // Configure cascade delete if needed

		builder.Navigation(x => x.AcceptedFormats).AutoInclude();
	}
}
