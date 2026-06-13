using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;

public class ReceiptNumberSequenceConfiguration : IEntityTypeConfiguration<ReceiptNumberSequence>
{
	public void Configure(EntityTypeBuilder<ReceiptNumberSequence> builder)
	{
		builder.ToTable("Finance_FinanceReceiptNumberSequences");

		builder.HasKey(s => s.Id);

		builder.Property(s => s.Id)
			.ValueGeneratedNever();

		// Map internal (non-nullable) sentinel columns — avoids PostgreSQL NULL-uniqueness issues.
		builder.Property(s => s.StoredTenantId)
			.HasColumnName("TenantId")
			.IsRequired();

		builder.Property(s => s.Year)
			.IsRequired();

		builder.Property(s => s.StoredKind)
			.HasColumnName("Kind")
			.IsRequired();

		builder.Property(s => s.NextValue)
			.IsRequired()
			.HasDefaultValue(1L);

		// Ignore the public nullable computed properties — they derive from the stored columns.
		builder.Ignore(s => s.TenantId);
		builder.Ignore(s => s.Kind);

		// Unique constraint on the three key columns (all non-nullable → standard unique index).
		builder.HasIndex(s => new { s.StoredTenantId, s.Year, s.StoredKind })
			.IsUnique()
			.HasDatabaseName("IX_FinanceReceiptNumberSequences_Tenant_Year_Kind");
	}
}
