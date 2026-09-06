using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionReversalConfiguration : IEntityTypeConfiguration<ConcessionReversal>
{
	public void Configure(EntityTypeBuilder<ConcessionReversal> builder)
	{
		builder.ToTable("Finance_ConcessionReversals");
		builder.ConfigureByConvention();
		builder.Property(x => x.Amount).HasPrecision(18, 2);
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(2000);
		builder.HasIndex(x => x.ConcessionRequestId);
		builder.HasIndex(x => x.ConcessionDueFeeAllocationId);
		builder.HasIndex(x => x.DueFeeId);
	}
}
