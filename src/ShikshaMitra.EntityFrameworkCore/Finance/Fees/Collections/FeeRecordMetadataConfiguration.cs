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

        builder.Property(x => x.ReceiptNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Remarks)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.ReceiptNumber })
            .IsUnique()
            .HasDatabaseName("UX_FeeRecordMetadatas_TenantId_ReceiptNumber");

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId })
            .HasDatabaseName("IX_FeeRecordMetadatas_StudentId_AcademicYearId");
    }
}
