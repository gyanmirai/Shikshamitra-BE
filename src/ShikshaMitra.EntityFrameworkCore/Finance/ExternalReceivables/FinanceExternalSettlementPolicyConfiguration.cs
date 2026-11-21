using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalSettlementPolicyConfiguration : IEntityTypeConfiguration<FinanceExternalSettlementPolicy>
{
	public void Configure(EntityTypeBuilder<FinanceExternalSettlementPolicy> builder)
	{
		builder.ToTable("Finance_ExternalSettlementPolicies", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Reason).HasMaxLength(1000).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.SourceLinkId }).IsUnique();
		builder.HasOne<FinanceExternalReceivableSourceLink>().WithMany().HasForeignKey(row => row.SourceLinkId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>().WithMany().HasForeignKey(row => row.CollectionPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>().WithMany().HasForeignKey(row => row.RefundPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
	}
}
