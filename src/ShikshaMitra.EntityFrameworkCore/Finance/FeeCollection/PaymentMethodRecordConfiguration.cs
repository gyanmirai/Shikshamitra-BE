using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.FeeCollection;

public class PaymentMethodRecordConfiguration : IEntityTypeConfiguration<PaymentMethodRecord>
{
    public void Configure(EntityTypeBuilder<PaymentMethodRecord> builder)
    {
        builder.ToTable("PaymentMethodRecords", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        // ─────────────────────────────────────────────────────────────────────
        // Store the polymorphic PaymentDetails as a JSON column.
        // EF Core 8+ deserializes the correct derived type at runtime using the
        // $type discriminator written by System.Text.Json ([JsonDerivedType]).
        // All payment-method-specific fields are stored in a single column —
        // no nullable columns, no discriminator column, no joins.
        // ─────────────────────────────────────────────────────────────────────
        builder.OwnsOne(x => x.PaymentDetails, pd => pd.ToJson("PaymentDetails"));

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_PaymentMethodRecords_StudentId");

        builder.HasIndex(x => x.FeeRecordMetadataId)
            .HasDatabaseName("IX_PaymentMethodRecords_FeeRecordMetadataId");
    }
}
