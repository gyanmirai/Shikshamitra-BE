using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionRequestDocumentConfiguration : IEntityTypeConfiguration<ConcessionRequestDocument>
{
	public void Configure(EntityTypeBuilder<ConcessionRequestDocument> builder)
	{
		builder.ToTable("Finance_ConcessionRequestDocuments");
		builder.ConfigureByConvention();

		builder.Property(x => x.Url).HasMaxLength(2048).IsRequired();

		builder.HasIndex(x => x.ConcessionRequestId)
			.HasDatabaseName("IX_ConcessionRequestDocuments_ConcessionRequestId");

		builder.HasIndex(x => new { x.ConcessionRequestId, x.DocumentId })
			.IsUnique()
			.HasDatabaseName("UIX_ConcessionRequestDocuments_RequestId_DocumentId");
	}
}
