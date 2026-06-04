using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.FeeAdjustment.Entities;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.FeeAdjustment;

public class FeeAdjustmentSourceLineConfiguration : IEntityTypeConfiguration<FeeAdjustmentSourceLine>
{
    public void Configure(EntityTypeBuilder<FeeAdjustmentSourceLine> builder)
    {
        builder.ToTable("Finance_FeeAdjustmentSourceLines", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.HasIndex(x => x.FeeAdjustmentRequestId)
            .HasDatabaseName("IX_FeeAdjustmentSourceLines_RequestId");

        builder.HasIndex(x => x.DueFeeId)
            .HasDatabaseName("IX_FeeAdjustmentSourceLines_DueFeeId");
    }
}
