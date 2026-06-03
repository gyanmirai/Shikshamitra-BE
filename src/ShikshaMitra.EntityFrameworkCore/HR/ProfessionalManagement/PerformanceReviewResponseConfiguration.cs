using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceReviewResponseConfiguration : IEntityTypeConfiguration<PerformanceReviewResponse>
{
	public void Configure(EntityTypeBuilder<PerformanceReviewResponse> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceReviewResponses", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Comment).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.StageId, x.CriterionId }).IsUnique();
	}
}
