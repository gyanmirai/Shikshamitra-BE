using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public class FeeAdjustmentTargetAllocationConfiguration : IEntityTypeConfiguration<FeeAdjustmentTargetAllocation>
{
    public void Configure(EntityTypeBuilder<FeeAdjustmentTargetAllocation> builder)
    {
        builder.ToTable("Finance_FeeAdjustmentTargetAllocations", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.HasIndex(x => x.FeeAdjustmentRequestId)
            .HasDatabaseName("IX_FeeAdjustmentTargetAllocations_RequestId");

        builder.HasIndex(x => x.DueFeeId)
            .HasDatabaseName("IX_FeeAdjustmentTargetAllocations_DueFeeId");
    }
}
