using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;

public class FeePaymentReceiptConfiguration : IEntityTypeConfiguration<FeePaymentReceipt>
{
    public void Configure(EntityTypeBuilder<FeePaymentReceipt> builder)
    {
        builder.ToTable("Finance_FeePaymentReceipts", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.ToTable(t => t.HasCheckConstraint("CK_FeePaymentReceipts_Payer", "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL"));
        builder.HasOne<ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeeAccount>().WithMany()
            .HasForeignKey(x => x.FinanceFeeAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ReceiptNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Remarks)
            .HasMaxLength(1000);

        builder.Property(x => x.PdfBlobName)
            .HasMaxLength(500);

        builder.Property(x => x.TotalPaidAmount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.ReceiptKind)
            .IsRequired();

        builder.Property(x => x.ReceiptScope)
            .IsRequired();

        builder.Property(x => x.VerificationStatus)
            .IsRequired();

        builder.Property(x => x.ReceiptDate)
            .IsRequired();

        builder.Property(x => x.BoardId)
            .IsRequired();

        builder.Property(x => x.EducationMediumId)
            .IsRequired();

        // ── Unique receipt number ─────────────────────────────────────────────
        builder.HasIndex(x => new { x.TenantId, x.ReceiptNumber })
            .IsUnique()
            .HasDatabaseName("UX_FeePaymentReceipts_TenantId_ReceiptNumber");

        // ── Parent transaction ────────────────────────────────────────────────
        builder.HasIndex(x => x.FeeCollectionTransactionId)
            .HasDatabaseName("IX_FeePaymentReceipts_FeeCollectionTransactionId");

        // ── List query performance ────────────────────────────────────────────
        builder.HasIndex(x => new
            {
                x.TenantId,
                x.AcademicYearId,
                x.BoardId,
                x.EducationMediumId,
                x.ReceiptKind,
            })
            .HasDatabaseName("IX_FeePaymentReceipts_ListFilter");

        // ── Reconciliation inbox query ────────────────────────────────────────
        builder.HasIndex(x => new
            {
                x.TenantId,
                x.AcademicYearId,
                x.ReceiptKind,
                x.VerificationStatus,
                x.ReceiptDate,
            })
            .HasDatabaseName("IX_FeePaymentReceipts_ReconciliationFilter");
    }
}
