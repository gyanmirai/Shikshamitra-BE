using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class DueFeeConfiguration : IEntityTypeConfiguration<DueFee>
{
	public void Configure(EntityTypeBuilder<DueFee> builder)
	{
		builder.ToTable("DueFees", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// Finance-critical: prevents duplicate due fee records at the database level
		// regardless of application-layer bugs or concurrent requests.
		builder.HasIndex(x => new { x.StudentId, x.FeeHeadId, x.Month, x.Year })
			.IsUnique();
	}
}
