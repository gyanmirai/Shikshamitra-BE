using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public class FeeRefundRequestConfiguration : IEntityTypeConfiguration<FeeRefundRequest>
{
    public void Configure(EntityTypeBuilder<FeeRefundRequest> builder)
    {
        builder.ToTable("Finance_FeeRefundRequests", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.VoucherNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RefundMethod).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.ApproverNotes).HasMaxLength(1000);

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
