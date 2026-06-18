using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Records;

public class StudentDashboardPreviewConfiguration : IEntityTypeConfiguration<StudentDashboardPreview>
{
	public void Configure(EntityTypeBuilder<StudentDashboardPreview> builder)
	{
		builder.ToTable("Student_DashboardPreviews");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.DashboardKey).IsRequired().HasMaxLength(32);
		builder.Property(x => x.ScopeKey).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(24);
		builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.LastError).HasMaxLength(2048);

		builder.HasIndex(x => new { x.TenantId, x.DashboardKey, x.ScopeKey })
			.IsUnique()
			.HasDatabaseName("IX_Student_DashboardPreviews_Tenant_Key_Scope");

		builder.HasIndex(x => new { x.Status, x.RefreshStartedAt })
			.HasDatabaseName("IX_Student_DashboardPreviews_Status_RefreshStartedAt");
	}
}
