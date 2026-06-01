using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Dashboard;

public class HrDashboardRefreshRunConfiguration : IEntityTypeConfiguration<HrDashboardRefreshRun>
{
	public void Configure(EntityTypeBuilder<HrDashboardRefreshRun> builder)
	{
		builder.ToTable("HR_DashboardRefreshRuns", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.RunKey)
			.IsRequired()
			.HasMaxLength(64);

		builder.Property(x => x.Status)
			.IsRequired()
			.HasMaxLength(24);

		builder.Property(x => x.LastError)
			.HasMaxLength(2048);

		builder.HasIndex(x => x.RunKey)
			.IsUnique()
			.HasDatabaseName("IX_HR_DashboardRefreshRuns_RunKey");
	}
}
