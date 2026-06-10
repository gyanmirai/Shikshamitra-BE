using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.FeeCollection;

public class FeeCollectionDueFeeAllocationConfiguration
    : IEntityTypeConfiguration<FeeCollectionDueFeeAllocation>
{
    public void Configure(EntityTypeBuilder<FeeCollectionDueFeeAllocation> builder)
    {
        builder.ToTable("FeeCollectionDueFeeAllocations", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Amount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.HasIndex(x => x.FeeRecordMetadataId)
            .HasDatabaseName("IX_FeeCollectionDueFeeAllocations_FeeRecordMetadataId");

        builder.HasIndex(x => x.DueFeeId)
            .HasDatabaseName("IX_FeeCollectionDueFeeAllocations_DueFeeId");
    }
}
