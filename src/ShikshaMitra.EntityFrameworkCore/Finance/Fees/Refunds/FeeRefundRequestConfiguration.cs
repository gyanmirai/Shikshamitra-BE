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
        builder.ConfigureByConvention();

        builder.Property(x => x.VoucherNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RefundMethod).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.ApproverNotes).HasMaxLength(1000);

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
            lb.ToTable("FeeRefundLines", ShikshaMitraConsts.DbSchema);
            lb.WithOwner().HasForeignKey("FeeRefundRequestId");
            lb.HasKey(l => l.Id);
            lb.Property(l => l.Amount).IsRequired();
            lb.HasIndex(l => l.DueFeeId)
                .HasDatabaseName("IX_FeeRefundLines_DueFeeId");
        });

        // One unique voucher per tenant
        builder.HasIndex(x => new { x.TenantId, x.VoucherNumber })
            .IsUnique()
            .HasDatabaseName("UX_FeeRefundRequests_TenantId_VoucherNumber");

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId, x.Status })
            .HasDatabaseName("IX_FeeRefundRequests_Student_Year_Status");
    }
}
