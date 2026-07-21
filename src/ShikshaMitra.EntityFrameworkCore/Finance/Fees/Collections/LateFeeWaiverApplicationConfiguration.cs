using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.FeeCollection;

public class LateFeeWaiverApplicationConfiguration : IEntityTypeConfiguration<LateFeeWaiverApplication>
{
	public void Configure(EntityTypeBuilder<LateFeeWaiverApplication> builder)
	{
		builder.ToTable("Finance_LateFeeWaiverApplications", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);
		builder.Property(x => x.ComputedLateFeeAmount).HasColumnType("numeric(18,2)").IsRequired();
		builder.Property(x => x.RequestedAmount).HasColumnType("numeric(18,2)").IsRequired();
		builder.Property(x => x.EffectiveAmount).HasColumnType("numeric(18,2)").IsRequired();

		builder.HasIndex(x => x.DueFeeId)
			.HasDatabaseName("IX_LateFeeWaiverApplications_DueFeeId");

		builder.HasIndex(x => new { x.TenantId, x.Status })
			.HasDatabaseName("IX_LateFeeWaiverApplications_Tenant_Status");
	}
}
