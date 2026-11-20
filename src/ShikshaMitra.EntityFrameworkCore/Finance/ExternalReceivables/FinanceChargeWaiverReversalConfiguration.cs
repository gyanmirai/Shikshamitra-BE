using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceChargeWaiverReversalConfiguration : IEntityTypeConfiguration<FinanceChargeWaiverReversal>
{
	public void Configure(EntityTypeBuilder<FinanceChargeWaiverReversal> builder)
	{
		builder.ToTable("Finance_ChargeWaiverReversals", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.TenantId).IsRequired();
		builder.Property(row => row.Amount).HasPrecision(18, 2);
		builder.Property(row => row.Currency).HasMaxLength(3).IsRequired();
		builder.Property(row => row.Reason).HasMaxLength(1000).IsRequired();
		builder.HasIndex(row => new { row.TenantId, row.WaiverApplicationId });
		builder.HasOne<ChargeWaiverApplication>().WithMany().HasForeignKey(row => row.WaiverApplicationId).OnDelete(DeleteBehavior.Restrict);
	}
}
