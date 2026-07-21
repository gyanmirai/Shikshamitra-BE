using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.FeeCollection;

public class LateFeeCollectionAllocationConfiguration : IEntityTypeConfiguration<LateFeeCollectionAllocation>
{
	public void Configure(EntityTypeBuilder<LateFeeCollectionAllocation> builder)
	{
		builder.ToTable("Finance_LateFeeCollectionAllocations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Amount)
			.HasColumnType("numeric(18,2)")
			.IsRequired();

		builder.HasIndex(x => x.FeeRecordMetadataId)
			.HasDatabaseName("IX_LateFeeCollectionAllocations_FeeRecordMetadataId");

		builder.HasIndex(x => x.DueFeeId)
			.HasDatabaseName("IX_LateFeeCollectionAllocations_DueFeeId");
	}
}
