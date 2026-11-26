using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Setup;

public sealed class HrSetupConfigurationRevisionMapping : IEntityTypeConfiguration<HrSetupConfigurationRevision>
{
	public void Configure(EntityTypeBuilder<HrSetupConfigurationRevision> builder)
	{
		builder.ToTable("HR_SetupConfigurationRevisions");
		builder.ConfigureByConvention();
		builder.HasKey(row => row.TenantId);
	}
}

public sealed class HrSetupReadinessSnapshotMapping : IEntityTypeConfiguration<HrSetupReadinessSnapshot>
{
	public void Configure(EntityTypeBuilder<HrSetupReadinessSnapshot> builder)
	{
		builder.ToTable("HR_SetupReadinessSnapshots");
		builder.ConfigureByConvention();
		builder.HasKey(row => new { row.TenantId, row.AcademicYearId });
	}
}
