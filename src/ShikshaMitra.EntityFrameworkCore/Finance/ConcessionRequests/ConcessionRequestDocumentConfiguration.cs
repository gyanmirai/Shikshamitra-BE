using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.ConcessionRequests;

namespace ShikshaMitra.EntityFrameworkCore.Finance.ConcessionRequests;

public class ConcessionRequestDocumentConfiguration : IEntityTypeConfiguration<ConcessionRequestDocument>
{
	public void Configure(EntityTypeBuilder<ConcessionRequestDocument> builder)
	{
		builder.ToTable("ConcessionRequestDocuments");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.ConcessionRequestId).IsRequired();
		builder.Property(x => x.ConcessionTypeId).IsRequired();
		builder.Property(x => x.DocumentId).IsRequired();
		builder.Property(x => x.Url).IsRequired().HasColumnType("text");
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.UploadedAt);
	}
}
