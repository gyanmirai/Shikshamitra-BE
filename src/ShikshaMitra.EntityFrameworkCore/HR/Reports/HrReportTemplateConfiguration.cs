using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Reports;

public class HrReportTemplateConfiguration : IEntityTypeConfiguration<HrReportTemplate>
{
	public void Configure(EntityTypeBuilder<HrReportTemplate> builder)
	{
		builder.ToTable("HR_ReportTemplates");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Description).HasMaxLength(512);
		builder.Property(x => x.Category).IsRequired().HasMaxLength(64);
		builder.Property(x => x.DataSource).IsRequired().HasMaxLength(64);
		builder.Property(x => x.Visibility).IsRequired().HasMaxLength(32);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
		builder.Property(x => x.DefinitionJson).IsRequired().HasColumnType("jsonb");

		builder.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.Name })
			.HasDatabaseName("IX_HR_ReportTemplates_Tenant_Owner_Name");

		builder.HasIndex(x => new { x.TenantId, x.Visibility, x.Status, x.DataSource })
			.HasDatabaseName("IX_HR_ReportTemplates_Tenant_Visibility_Status_Source");
	}
}
