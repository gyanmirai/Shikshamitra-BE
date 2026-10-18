using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Reports;

public class HrReportRunConfiguration : IEntityTypeConfiguration<HrReportRun>
{
	public void Configure(EntityTypeBuilder<HrReportRun> b)
	{
		b.ToTable("HR_ReportRuns"); b.ConfigureByConvention();
		b.Property(x => x.Name).HasMaxLength(128).IsRequired();
		b.Property(x => x.TemplateId).HasMaxLength(128).IsRequired();
		b.Property(x => x.DataSource).HasMaxLength(64).IsRequired();
		b.Property(x => x.Status).HasMaxLength(32).IsRequired();
		b.Property(x => x.ExcelStatus).HasMaxLength(32).IsRequired();
		b.Property(x => x.PdfStatus).HasMaxLength(32).IsRequired();
		b.Property(x => x.ErrorMessage).HasMaxLength(512);
		b.Property(x => x.DefinitionJson).HasColumnType("jsonb").IsRequired();
		b.Property(x => x.ParametersJson).HasColumnType("jsonb").IsRequired();
		b.Property(x => x.ScopeJson).HasColumnType("jsonb").IsRequired();
		b.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
		b.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.CreationTime });
	}
}

public class HrReportRunPageConfiguration : IEntityTypeConfiguration<HrReportRunPage>
{
	public void Configure(EntityTypeBuilder<HrReportRunPage> b)
	{
		b.ToTable("HR_ReportRunPages"); b.ConfigureByConvention();
		b.Property(x => x.RowsJson).HasColumnType("jsonb").IsRequired();
		b.HasIndex(x => new { x.TenantId, x.RunId, x.PageNumber }).IsUnique();
		b.HasIndex(x => x.ExpiresAt);
		b.HasOne<HrReportRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
	}
}