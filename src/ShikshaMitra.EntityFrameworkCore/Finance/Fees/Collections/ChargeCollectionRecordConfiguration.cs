using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class ChargeCollectionRecordConfiguration : IEntityTypeConfiguration<ChargeCollectionRecord>
{
    public void Configure(EntityTypeBuilder<ChargeCollectionRecord> builder)
    {
        builder.ToTable("Finance_ChargeCollectionRecords", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.HasIndex(x => x.FeeRecordMetadataId)
            .HasDatabaseName("IX_ChargeCollectionRecords_FeeRecordMetadataId");

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_ChargeCollectionRecords_StudentId");
    }
}
