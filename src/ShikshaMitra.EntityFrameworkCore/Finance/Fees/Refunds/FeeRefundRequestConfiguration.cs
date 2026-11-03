using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public class FeeRefundRequestConfiguration : IEntityTypeConfiguration<FeeRefundRequest>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public void Configure(EntityTypeBuilder<FeeRefundRequest> builder)
    {
        builder.ToTable("Finance_FeeRefundRequests", ShikshaMitraConsts.DbSchema);
        builder.Property(x=>x.ExpenseCategoryName).HasMaxLength(500);
        builder.Property(x=>x.PayingBankLabel).HasMaxLength(500);
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.TransactionCategory>().WithMany().HasForeignKey(x=>x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount>().WithMany().HasForeignKey(x=>x.PayingBankAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.VoucherNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OriginalReceiptNumber).HasMaxLength(100);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.RefundMethod).HasMaxLength(100);
        builder.Property(x => x.RequestIdempotencyKey).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.ApproverNotes).HasMaxLength(1000);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.ReversalReason).HasMaxLength(1000);
        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CalculatedAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ApprovedAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ProcessedAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ReversedAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PolicySnapshotJson).HasColumnType("jsonb");
        builder.Property(x => x.CalculationSnapshotJson).HasColumnType("jsonb");
        builder.Property(x => x.CalculationHash).HasMaxLength(64);

        // ── Polymorphic RefundPaymentDetails stored as jsonb ──────────────────
        // System.Text.Json handles polymorphism via the [JsonDerivedType] discriminators
        // declared on the abstract RefundPaymentDetails base class, writing and reading
        // the "$type" discriminator automatically.
        builder.Property(x => x.RefundPaymentDetails)
            .HasColumnName("RefundPaymentDetails")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null
                    ? null
                    : JsonSerializer.Serialize(v, _jsonOptions),
                v => v == null
                    ? null
                    : JsonSerializer.Deserialize<RefundPaymentDetails>(v, _jsonOptions))
            .IsRequired(false);

        builder.OwnsMany(r => r.Lines, lb =>
        {
            lb.ToTable("Finance_FeeRefundLines", ShikshaMitraConsts.DbSchema);
            lb.WithOwner().HasForeignKey("FeeRefundRequestId");
            lb.HasKey(l => l.Id);
            // The domain assigns replacement line IDs; EF must insert these new owned rows.
            lb.Property(l => l.Id).ValueGeneratedNever();
            lb.Property(l => l.FeeHeadName).HasMaxLength(200);
            lb.Property(l => l.InstallmentLabel).HasMaxLength(200);
            lb.Property(l => l.SettledPaidAmount).HasColumnType("numeric(18,2)");
            lb.Property(l => l.RequestedAmount).HasColumnName("Amount").HasColumnType("numeric(18,2)");
            lb.Property(l => l.CalculatedAmount).HasColumnType("numeric(18,2)");
            lb.Property(l => l.ApprovedAmount).HasColumnType("numeric(18,2)");
            lb.Property(l => l.ProcessedAmount).HasColumnType("numeric(18,2)");
            lb.Property(l => l.ReversedAmount).HasColumnType("numeric(18,2)");
            lb.Ignore(l => l.Amount);
            lb.HasIndex(l => l.ChargeId);
            lb.HasIndex(l => l.LateFeeDueFeeId);
            lb.ToTable(t => t.HasCheckConstraint("CK_FeeRefundLines_Source",
                "(CASE WHEN \"DueFeeId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"ChargeId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"LateFeeDueFeeId\" IS NULL THEN 0 ELSE 1 END) = 1"));
            lb.HasIndex(l => l.DueFeeId)
                .HasDatabaseName("IX_FeeRefundLines_DueFeeId");
        });

        builder.OwnsMany(r => r.Reservations, rb =>
        {
            rb.ToTable("Finance_FeeRefundReservations", ShikshaMitraConsts.DbSchema);
            rb.WithOwner().HasForeignKey("FeeRefundRequestId");
            rb.HasKey(x => x.Id);
            rb.HasIndex(x => new { x.ChargeId, x.Status });
            rb.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            rb.HasIndex(x => new { x.DueFeeId, x.Status });
            rb.HasIndex(x => new { x.FeeRefundRequestId, x.FeeRefundLineId }).IsUnique();
        });

        builder.OwnsMany(r => r.Settlements, sb =>
        {
            sb.ToTable("Finance_FeeRefundSettlements", ShikshaMitraConsts.DbSchema);
            sb.WithOwner().HasForeignKey("FeeRefundRequestId");
            sb.HasKey(x => x.Id);
            sb.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            sb.Property(x => x.Method).HasMaxLength(100).IsRequired();
            sb.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            sb.Property(x => x.ExternalReference).HasMaxLength(200);
            sb.Property(x => x.PaymentDetailsJson).HasColumnType("jsonb");
            sb.Property(x => x.FailureReason).HasMaxLength(1000);
            sb.Property(x => x.ReversalReason).HasMaxLength(1000);
            sb.Property(x => x.EvidenceReference).HasMaxLength(500);
            sb.Property(x => x.VerificationKey).HasMaxLength(100);
            sb.HasIndex(x => new { x.FeeRefundRequestId, x.IdempotencyKey }).IsUnique();
        });

        // One unique voucher per tenant
        builder.HasIndex(x => new { x.TenantId, x.VoucherNumber })
            .IsUnique()
            .HasDatabaseName("UX_FeeRefundRequests_TenantId_VoucherNumber");

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId, x.Status })
            .HasDatabaseName("IX_FeeRefundRequests_Student_Year_Status");
        builder.HasIndex(x => x.OriginalReceiptId);
        builder.HasIndex(x => new { x.TenantId, x.RequestIdempotencyKey }).IsUnique()
            .HasFilter("\"RequestIdempotencyKey\" IS NOT NULL");
        builder.HasIndex(x => x.ReplacesRefundRequestId).IsUnique()
            .HasFilter("\"ReplacesRefundRequestId\" IS NOT NULL");
        builder.HasIndex(x => x.RefundPolicyVersionId);
        builder.HasIndex(x => new { x.ConcessionRequestId, x.OriginalReceiptId })
            .IsUnique()
            .HasFilter("\"ConcessionRequestId\" IS NOT NULL AND \"Status\" NOT IN (3, 8, 9)")
            .HasDatabaseName("UX_FeeRefundRequests_Concession_Receipt");

        builder.ConfigureByConvention();
        builder.ToTable(t => t.HasCheckConstraint("CK_FeeRefundRequests_Payer", "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL"));
        builder.HasOne<ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeeAccount>().WithMany()
            .HasForeignKey(x => x.FinanceFeeAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
