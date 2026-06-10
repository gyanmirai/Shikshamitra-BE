using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionRequestConfiguration : IEntityTypeConfiguration<ConcessionRequest>
{
	public void Configure(EntityTypeBuilder<ConcessionRequest> builder)
	{
		builder.ToTable("AppConcessionRequests");
		builder.ConfigureByConvention();

		builder.Property(x => x.Remarks).HasMaxLength(2000);
		builder.Property(x => x.RejectionReason).HasMaxLength(2000);
		builder.Property(x => x.ApproverMessage).HasMaxLength(1000);

		builder.HasMany(x => x.ConcessionFeeHeadMappings)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.ConcessionRequestDocuments)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.ConcessionDueFeeAllocations)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(x => x.ConcessionFeeHeadMappings).AutoInclude();
		builder.Navigation(x => x.ConcessionRequestDocuments).AutoInclude();
		builder.Navigation(x => x.ConcessionDueFeeAllocations).AutoInclude();
	}
}
