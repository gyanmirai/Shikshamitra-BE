using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Dashboard;

public class HrDashboardPreviewConfiguration : IEntityTypeConfiguration<HrDashboardPreview>
{
	public void Configure(EntityTypeBuilder<HrDashboardPreview> builder)
	{
		builder.ToTable("HR_DashboardPreviews");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.DashboardKey).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(24);
		builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.LastError).HasMaxLength(2048);

		builder.HasIndex(x => new { x.TenantId, x.DashboardKey })
			.IsUnique()
			.HasDatabaseName("IX_HR_DashboardPreviews_Tenant_Key");

		builder.HasIndex(x => new { x.Status, x.RefreshStartedAt })
			.HasDatabaseName("IX_HR_DashboardPreviews_Status_RefreshStartedAt");
	}
}
