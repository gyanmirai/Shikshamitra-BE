using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.ConcessionRequests;

public class ConcessionRequestConfiguration : IEntityTypeConfiguration<ConcessionRequest>
{
	public void Configure(EntityTypeBuilder<ConcessionRequest> builder)
	{
		builder.ToTable("ConcessionRequests");
		builder.ConfigureByConvention();

		builder.Property(x => x.CompletedStepsRaw).HasColumnType("text");

		builder.HasMany(x => x.ConcessionRequestConcessionTypes)
			.WithOne()
			.HasForeignKey(x => x.ConcessionRequestId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
