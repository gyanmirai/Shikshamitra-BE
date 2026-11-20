using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library.Books;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalReceivableSourceLinkConfiguration : IEntityTypeConfiguration<FinanceExternalReceivableSourceLink>
{
	public void Configure(EntityTypeBuilder<FinanceExternalReceivableSourceLink> builder)
	{
		builder.ToTable("Finance_ExternalReceivableSourceLinks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.LegacyConcurrencyStamp).HasColumnName("ConcurrencyStamp").HasMaxLength(40).IsRequired();
		builder.Property(row => row.LegacyExtraPropertiesJson).HasColumnName("ExtraProperties").HasColumnType("text").IsRequired();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.SourceVersion).HasMaxLength(64).IsRequired();
		builder.Property(row => row.SourceFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(row => row.SourceSnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(row => row.Currency).HasMaxLength(3).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.SourceKind, row.SourceId }).IsUnique()
			.HasDatabaseName("UX_ExternalReceivableSources_Tenant_Source");
		builder.HasIndex(row => new { row.TenantId, row.FinanceChargeId }).IsUnique()
			.HasDatabaseName("UX_ExternalReceivableSources_Tenant_Charge");
		builder.HasIndex(row => new { row.TenantId, row.FinanceChargeRequestId }).IsUnique()
			.HasDatabaseName("UX_ExternalReceivableSources_Tenant_Request");
		builder.HasOne<LibraryCharge>().WithMany().HasForeignKey(row => row.SourceId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Charge>().WithMany().HasForeignKey(row => row.FinanceChargeId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<ChargeRequest>().WithMany().HasForeignKey(row => row.FinanceChargeRequestId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>().WithMany().HasForeignKey(row => row.CollectionPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>().WithMany().HasForeignKey(row => row.RefundPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
	}
}
