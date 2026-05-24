using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Audit;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Audit;

public class HrAuditEventConfiguration : IEntityTypeConfiguration<HrAuditEvent>
{
	public void Configure(EntityTypeBuilder<HrAuditEvent> builder)
	{
		builder.ToTable("HR_AuditEvents");
		builder.ConfigureByConvention();

		builder.Property(x => x.EntityType).IsRequired().HasMaxLength(64);
		builder.Property(x => x.EntityId).IsRequired();
		builder.Property(x => x.EntityName).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Action).IsRequired().HasMaxLength(64);
		builder.Property(x => x.ActorNameSnapshot).HasMaxLength(128);
		builder.Property(x => x.OccurredAt).IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(512);
		builder.Property(x => x.Summary).IsRequired().HasMaxLength(512);
		builder.Property(x => x.ChangedFieldsJson).HasColumnType("text");
		builder.Property(x => x.ContextJson).HasColumnType("text");
		builder.Property(x => x.CorrelationId).HasMaxLength(128);

		builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.OccurredAt })
			.HasDatabaseName("IX_HR_AuditEvents_Tenant_Entity_Time");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.OccurredAt })
			.HasDatabaseName("IX_HR_AuditEvents_Tenant_AcademicYear_Time");
		builder.HasIndex(x => new { x.TenantId, x.Action, x.OccurredAt })
			.HasDatabaseName("IX_HR_AuditEvents_Tenant_Action_Time");
	}
}
