using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleConfiguration : IEntityTypeConfiguration<HrReportSchedule>
{
	public void Configure(EntityTypeBuilder<HrReportSchedule> builder)
	{
		builder.ToTable("HR_ReportSchedules");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Frequency).IsRequired().HasMaxLength(24);
		builder.Property(x => x.DeliveryMode).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(24);
		builder.Property(x => x.RecipientUserIdsJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.DefinitionJson).IsRequired().HasColumnType("jsonb");

		builder.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.Status, x.NextRunAt })
			.HasDatabaseName("IX_HR_ReportSchedules_Tenant_Owner_Status_NextRun");

		builder.HasIndex(x => new { x.TenantId, x.Status, x.NextRunAt })
			.HasDatabaseName("IX_HR_ReportSchedules_Tenant_Status_NextRun");
	}
}
