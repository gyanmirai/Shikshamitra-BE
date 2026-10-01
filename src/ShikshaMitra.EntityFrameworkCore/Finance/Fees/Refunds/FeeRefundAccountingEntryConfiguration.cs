using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public class FeeRefundAccountingEntryConfiguration : IEntityTypeConfiguration<FeeRefundAccountingEntry>
{
    public void Configure(EntityTypeBuilder<FeeRefundAccountingEntry> builder)
    {
        builder.ToTable("Finance_FeeRefundAccountingEntries", ShikshaMitraConsts.DbSchema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Method).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(200);
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.FeeHeadAmountsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.RefundRequestId, x.IsCorrection }).IsUnique()
            .HasDatabaseName("IX_FeeRefundAccountingEntries_Tenant_Request_Correction");
        builder.HasIndex(x => x.SettlementId).HasDatabaseName("IX_FeeRefundAccountingEntries_SettlementId");
        builder.HasIndex(x => x.OriginalReceiptId).HasDatabaseName("IX_FeeRefundAccountingEntries_OriginalReceiptId");
    }
}
