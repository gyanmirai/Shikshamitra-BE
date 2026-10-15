using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class PaymentMethodRecordConfiguration : IEntityTypeConfiguration<PaymentMethodRecord>
{
    // Reuse a single options instance — it honours the [JsonDerivedType] attributes
    // on PaymentDetails, so the correct concrete subclass is round-tripped via the
    // "$type" discriminator without any extra EF Core owned-entity modelling.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public void Configure(EntityTypeBuilder<PaymentMethodRecord> builder)
    {
        builder.ToTable("Finance_PaymentMethodRecords", ShikshaMitraConsts.DbSchema);
        builder.Property(x=>x.BankLabelSnapshot).HasMaxLength(500);
        builder.HasOne<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount>().WithMany().HasForeignKey(x=>x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.ConfigureByConvention();
        builder.ToTable(t => t.HasCheckConstraint("CK_PaymentMethodRecords_Payer", "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL"));
        builder.HasOne<FinanceFeeAccount>().WithMany().HasForeignKey(x => x.FinanceFeeAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Amount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        // ─────────────────────────────────────────────────────────────────────
        // Store the polymorphic PaymentDetails hierarchy as a single JSON string
        // column using a value converter.  This avoids the design-time error that
        // arises from OwnsOne().ToJson() when the mapped CLR type is abstract:
        //   "The corresponding CLR type … cannot be instantiated, and there is no
        //    derived entity type in the model that corresponds to a concrete CLR type."
        //
        // System.Text.Json handles polymorphism through the [JsonDerivedType]
        // attributes declared on the abstract PaymentDetails class, writing and
        // reading the "$type" discriminator automatically.
        // ─────────────────────────────────────────────────────────────────────
        // jsonb gives free binary parsing, JSON operator support and GIN indexing.
        // System.Text.Json handles polymorphic serialisation via the [JsonDerivedType]
        // discriminators on the abstract PaymentDetails base class.
        builder.Property(x => x.PaymentDetails)
            .HasColumnName("PaymentDetails")
            .HasColumnType("jsonb")
            .HasConversion(
				v => v == null
                    ? null
                    : JsonSerializer.Serialize(v, _jsonOptions),
                v => v == null
                    ? null
                    : JsonSerializer.Deserialize<PaymentDetails>(v, _jsonOptions))
            .IsRequired(false);

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_PaymentMethodRecords_StudentId");

        builder.HasIndex(x => x.FeeRecordMetadataId)
            .HasDatabaseName("IX_PaymentMethodRecords_FeeRecordMetadataId");
    }
}

