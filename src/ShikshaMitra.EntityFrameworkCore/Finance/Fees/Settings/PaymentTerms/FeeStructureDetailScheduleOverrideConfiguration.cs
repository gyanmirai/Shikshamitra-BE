using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.PaymentTerms;

public class FeeStructureDetailScheduleOverrideConfiguration
	: IEntityTypeConfiguration<FeeStructureDetailScheduleOverride>
{
	public void Configure(EntityTypeBuilder<FeeStructureDetailScheduleOverride> builder)
	{
		builder.ToTable("Finance_FeeStructureDetailScheduleOverrides", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.AllocationPercentage).HasPrecision(18, 4);

		builder.HasIndex(x => new { x.TenantId, x.FeeStructureDetailId, x.Sequence });
	}
}
