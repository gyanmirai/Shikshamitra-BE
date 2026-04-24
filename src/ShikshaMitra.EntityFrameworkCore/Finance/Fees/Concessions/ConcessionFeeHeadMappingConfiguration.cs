using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Concessions;

public class ConcessionFeeHeadMappingConfiguration : IEntityTypeConfiguration<ConcessionFeeHeadMapping>
{
    public void Configure(EntityTypeBuilder<ConcessionFeeHeadMapping> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ConcessionFeeHeadMappings", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.ConcessionRequestId)
            .IsRequired();

        builder.Property(x => x.FeeHeadId)
            .IsRequired();

        builder.Property(x => x.FeeHeadConcessionAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.PreConcessionFeeHeadAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.PostConcessionFeeHeadAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.UserSpecifiedConcessionAmountValue)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.UserSpecifiedConcessionAmountType)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.ConcessionRequestId)
            .HasDatabaseName("IX_ConcessionFeeHeadMappings_ConcessionRequestId");

        builder.HasIndex(x => new { x.ConcessionRequestId, x.FeeHeadId })
            .IsUnique()
            .HasDatabaseName("UIX_ConcessionFeeHeadMappings_RequestId_FeeHeadId");
    }
}
