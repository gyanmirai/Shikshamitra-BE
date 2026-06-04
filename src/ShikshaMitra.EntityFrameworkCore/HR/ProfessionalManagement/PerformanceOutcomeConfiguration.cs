using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceOutcomeConfiguration : IEntityTypeConfiguration<PerformanceOutcome>
{
	public void Configure(EntityTypeBuilder<PerformanceOutcome> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceOutcomes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.FinalScore).HasPrecision(7, 2);
		builder.Property(x => x.FinalRating).IsRequired().HasMaxLength(80);
		builder.Property(x => x.Strengths).HasMaxLength(2000);
		builder.Property(x => x.ImprovementAreas).HasMaxLength(2000);
		builder.Property(x => x.RecommendationNotes).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId }).IsUnique();
	}
}
