using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.ConcessionRequests;

public class ConcessionRequestFeeHeadValueConfiguration : IEntityTypeConfiguration<ConcessionRequestFeeHeadValue>
{
	public void Configure(EntityTypeBuilder<ConcessionRequestFeeHeadValue> builder)
	{
		builder.ToTable("ConcessionRequestFeeHeadValues");
		builder.ConfigureByConvention();

		builder.Property(x => x.ConcessionRequestId).IsRequired();
		builder.Property(x => x.ConcessionTypeId).IsRequired();
		builder.Property(x => x.FeeHeadId).IsRequired();
		builder.Property(x => x.BasedOn).IsRequired();
		builder.Property(x => x.Amount).IsRequired();
		builder.Property(x => x.DiscountAmount).IsRequired();
		builder.Property(x => x.NetAmount).IsRequired();

		builder.HasIndex(x => new { x.ConcessionRequestId, x.ConcessionTypeId, x.FeeHeadId }).IsUnique();
	}
}
