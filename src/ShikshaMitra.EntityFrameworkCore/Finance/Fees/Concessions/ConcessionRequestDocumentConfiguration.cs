using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Concessions;

public class ConcessionRequestDocumentConfiguration : IEntityTypeConfiguration<ConcessionRequestDocument>
{
    public void Configure(EntityTypeBuilder<ConcessionRequestDocument> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ConcessionRequestDocuments", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ConcessionRequestId)
            .IsRequired();

        builder.Property(x => x.DocumentId)
            .IsRequired();

        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(x => x.Status)
            .IsRequired();

        // Unique: one upload record per document slot per concession request
        builder.HasIndex(x => new { x.ConcessionRequestId, x.DocumentId })
            .IsUnique()
            .HasDatabaseName("UIX_ConcessionRequestDocuments_RequestId_DocumentId");

        builder.HasIndex(x => x.ConcessionRequestId)
            .HasDatabaseName("IX_ConcessionRequestDocuments_ConcessionRequestId");
    }
}
