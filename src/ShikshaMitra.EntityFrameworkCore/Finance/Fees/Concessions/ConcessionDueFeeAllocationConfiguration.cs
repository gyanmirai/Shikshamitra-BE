using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionDueFeeAllocationConfiguration : IEntityTypeConfiguration<ConcessionDueFeeAllocation>
{
	public void Configure(EntityTypeBuilder<ConcessionDueFeeAllocation> builder)
	{
		builder.ToTable("Finance_ConcessionDueFeeAllocations");
		builder.ConfigureByConvention();

		builder.Property(x => x.ConcessionAmount).HasColumnType("decimal(18,2)");

		builder.HasIndex(x => x.ConcessionRequestId)
			.HasDatabaseName("IX_ConcessionDueFeeAllocations_ConcessionRequestId");

		builder.HasIndex(x => x.DueFeeId)
			.HasDatabaseName("IX_ConcessionDueFeeAllocations_DueFeeId");

		builder.HasIndex(x => new { x.ConcessionRequestId, x.DueFeeId })
			.IsUnique()
			.HasDatabaseName("UIX_ConcessionDueFeeAllocations_Request_DueFee");
	}
}
