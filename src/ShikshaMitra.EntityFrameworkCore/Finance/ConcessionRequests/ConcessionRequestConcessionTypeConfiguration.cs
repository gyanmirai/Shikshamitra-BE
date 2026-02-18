using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.ConcessionRequests;

public class ConcessionRequestConcessionTypeConfiguration : IEntityTypeConfiguration<ConcessionRequestConcessionType>
{
	public void Configure(EntityTypeBuilder<ConcessionRequestConcessionType> builder)
	{
		builder.ToTable("ConcessionRequestConcessionTypes");
		builder.ConfigureByConvention();

		builder.Property(x => x.ConcessionRequestId).IsRequired();
		builder.Property(x => x.ConcessionTypeId).IsRequired();

		builder.HasMany(x => x.FeeHeadMappings)
			.WithOne()
			.HasForeignKey(x => new { x.ConcessionRequestId, x.ConcessionTypeId })
			.HasPrincipalKey(x => new { x.ConcessionRequestId, x.ConcessionTypeId })
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.ConcessionRequestId, x.ConcessionTypeId }).IsUnique();
	}
}
