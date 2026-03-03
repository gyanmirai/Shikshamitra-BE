using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.ConcessionRequests;

public class ConcessionRequestFeeHeadMappingConfiguration : IEntityTypeConfiguration<ConcessionRequestFeeHeadMapping>
{
	public void Configure(EntityTypeBuilder<ConcessionRequestFeeHeadMapping> builder)
	{
		builder.ToTable("ConcessionRequestFeeHeadMappings");
		builder.ConfigureByConvention();

		builder.Property(x => x.ConcessionRequestId).IsRequired();
		builder.Property(x => x.ConcessionTypeId).IsRequired();
		builder.Property(x => x.FeeHeadId).IsRequired();

		builder.HasIndex(x => new { x.ConcessionRequestId, x.ConcessionTypeId, x.FeeHeadId }).IsUnique();
	}
}
