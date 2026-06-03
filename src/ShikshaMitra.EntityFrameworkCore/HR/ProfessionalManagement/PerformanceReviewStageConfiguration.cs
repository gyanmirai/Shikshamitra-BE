using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceReviewStageConfiguration : IEntityTypeConfiguration<PerformanceReviewStage>
{
	public void Configure(EntityTypeBuilder<PerformanceReviewStage> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceReviewStages", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.SummaryComment).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.StageType }).IsUnique();
	}
}
