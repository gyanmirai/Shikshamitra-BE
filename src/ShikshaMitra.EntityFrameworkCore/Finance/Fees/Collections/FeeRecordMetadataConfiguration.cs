using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class FeeRecordMetadataConfiguration : IEntityTypeConfiguration<FeeRecordMetadata>
{
    public void Configure(EntityTypeBuilder<FeeRecordMetadata> builder)
    {
        builder.ToTable("Finance_FeeRecordMetadatas", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.ToTable(t => t.HasCheckConstraint("CK_FeeRecordMetadatas_Payer", ShikshaMitra.Finance.ExternalReceivables.FinancePayerConstraintSql.StudentAccountOrEmployee));
        builder.Property(row => row.TotalPaidFee).HasPrecision(18, 2);
        builder.Property(row => row.Currency).HasMaxLength(3);
        builder.HasOne<ShikshaMitra.HR.Employees.Employee>().WithMany().HasForeignKey(row => row.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.TenantId, row.EmployeeId, row.AcademicYearId }).HasFilter("\"EmployeeId\" IS NOT NULL");
        builder.HasOne<FinanceFeeAccount>().WithMany().HasForeignKey(x => x.FinanceFeeAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ReceiptNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Remarks)
            .HasMaxLength(1000);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(100);

        builder.Property(x => x.CommandFingerprint)
            .HasMaxLength(64);

        builder.Property(x => x.CommandResultJson)
            .HasColumnType("text");

        builder.HasIndex(x => new { x.TenantId, x.ReceiptNumber })
            .IsUnique()
            .HasDatabaseName("UX_FeeRecordMetadatas_TenantId_ReceiptNumber");

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId })
            .HasDatabaseName("IX_FeeRecordMetadatas_StudentId_AcademicYearId");

        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL AND \"IsDeleted\" = false")
            .HasDatabaseName("UX_FeeRecordMetadatas_TenantId_IdempotencyKey");
    }
}
