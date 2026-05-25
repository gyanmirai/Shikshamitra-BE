using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportJobConfiguration : IEntityTypeConfiguration<HrReportExportJob>
{
	public void Configure(EntityTypeBuilder<HrReportExportJob> builder)
	{
		builder.ToTable("HR_ReportExportJobs");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.ReportType).IsRequired().HasMaxLength(96);
		builder.Property(x => x.Format).IsRequired().HasMaxLength(16);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
		builder.Property(x => x.BlobName).HasMaxLength(512);
		builder.Property(x => x.ErrorMessage).HasMaxLength(2048);
		builder.Property(x => x.FilterJson).IsRequired().HasColumnType("jsonb");

		builder.HasIndex(x => new { x.TenantId, x.RequestedByUserId, x.CreationTime })
			.HasDatabaseName("IX_HR_ReportExportJobs_Tenant_User_Created");

		builder.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt })
			.HasDatabaseName("IX_HR_ReportExportJobs_Tenant_Status_Expires");
	}
}
