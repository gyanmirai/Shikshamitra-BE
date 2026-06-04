using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceAuditEventConfiguration : IEntityTypeConfiguration<PerformanceAuditEvent>
{
	public void Configure(EntityTypeBuilder<PerformanceAuditEvent> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceAuditEvents", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.EventType).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Summary).IsRequired().HasMaxLength(500);
		builder.Property(x => x.MetadataJson).HasMaxLength(4000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.CreationTime });
	}
}
