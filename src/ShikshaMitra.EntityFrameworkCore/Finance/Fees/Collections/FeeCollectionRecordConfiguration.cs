using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class FeeCollectionRecordConfiguration : IEntityTypeConfiguration<FeeCollectionRecord>
{
    public void Configure(EntityTypeBuilder<FeeCollectionRecord> builder)
    {
        builder.ToTable("Finance_FeeCollectionRecords", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.ToTable(t => t.HasCheckConstraint("CK_FeeCollectionRecords_Payer", "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL"));
        builder.HasOne<FinanceFeeAccount>().WithMany().HasForeignKey(x => x.FinanceFeeAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.FeeRecordMetadataId)
            .HasDatabaseName("IX_FeeCollectionRecords_FeeRecordMetadataId");

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_FeeCollectionRecords_StudentId");
    }
}
