using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public class FeeAdjustmentRequestConfiguration : IEntityTypeConfiguration<FeeAdjustmentRequest>
{
    public void Configure(EntityTypeBuilder<FeeAdjustmentRequest> builder)
    {
        builder.ToTable("Finance_FeeAdjustmentRequests", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(2000);
        builder.Property(x => x.VoucherNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.Property(x => x.ApproverNotes).HasMaxLength(2000);
        builder.Property(x => x.LastIdempotencyKey).HasMaxLength(64);

        builder.HasMany(x => x.SourceLines)
            .WithOne()
            .HasForeignKey(x => x.FeeAdjustmentRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.TargetAllocations)
            .WithOne()
            .HasForeignKey(x => x.FeeAdjustmentRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.SourceLines).AutoInclude();
        builder.Navigation(x => x.TargetAllocations).AutoInclude();

        builder.HasIndex(x => new { x.TenantId, x.VoucherNumber })
            .IsUnique()
            .HasDatabaseName("UX_FeeAdjustmentRequests_Tenant_Voucher");

        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.BoardId, x.EducationMediumId, x.Status })
            .HasDatabaseName("IX_FeeAdjustmentRequests_Inbox");
    }
}
