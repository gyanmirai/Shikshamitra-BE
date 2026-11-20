using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalWaiverRequestConfiguration : IEntityTypeConfiguration<FinanceExternalWaiverRequest>
{
	public void Configure(EntityTypeBuilder<FinanceExternalWaiverRequest> builder)
	{
		builder.ToTable("Finance_ExternalWaiverRequests", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Amount).HasPrecision(18, 2);
		builder.Property(row => row.Currency).HasMaxLength(3).IsRequired();
		builder.Property(row => row.Reason).HasMaxLength(1000).IsRequired();
		builder.Property(row => row.DecisionReason).HasMaxLength(1000);
		builder.HasIndex(row => new { row.TenantId, row.SourceLinkId, row.State });
		builder.HasIndex(row => row.AppliedWaiverId).IsUnique();
		builder.HasOne<FinanceExternalReceivableSourceLink>().WithMany().HasForeignKey(row => row.SourceLinkId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<FinancePolicyVersion>().WithMany().HasForeignKey(row => row.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<ChargeWaiverApplication>().WithMany().HasForeignKey(row => row.AppliedWaiverId).OnDelete(DeleteBehavior.Restrict);
	}
}
