using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class ChargeCollectionRecordConfiguration : IEntityTypeConfiguration<ChargeCollectionRecord>
{
    public void Configure(EntityTypeBuilder<ChargeCollectionRecord> builder)
    {
        builder.ToTable("Finance_ChargeCollectionRecords", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
        builder.Property(row => row.Amount).HasPrecision(18, 2);
        builder.ToTable(table => table.HasCheckConstraint("CK_ChargeCollectionRecords_Payer", ShikshaMitra.Finance.ExternalReceivables.FinancePayerConstraintSql.StudentOrEmployee));
        builder.HasOne<ShikshaMitra.HR.Employees.Employee>().WithMany().HasForeignKey(row => row.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => new { row.TenantId, row.EmployeeId }).HasFilter("\"EmployeeId\" IS NOT NULL");

        builder.HasIndex(x => x.FeeRecordMetadataId)
            .HasDatabaseName("IX_ChargeCollectionRecords_FeeRecordMetadataId");

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_ChargeCollectionRecords_StudentId");
    }
}
