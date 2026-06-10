using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

public class ConcessionFeeHeadMappingConfiguration : IEntityTypeConfiguration<ConcessionFeeHeadMapping>
{
	public void Configure(EntityTypeBuilder<ConcessionFeeHeadMapping> builder)
	{
		builder.ToTable("AppConcessionFeeHeadMappings");
		builder.ConfigureByConvention();

		builder.HasIndex(x => x.ConcessionRequestId)
			.HasDatabaseName("IX_ConcessionFeeHeadMappings_ConcessionRequestId");

		builder.HasIndex(x => new { x.ConcessionRequestId, x.FeeHeadId })
			.IsUnique()
			.HasDatabaseName("UIX_ConcessionFeeHeadMappings_RequestId_FeeHeadId");
	}
}
