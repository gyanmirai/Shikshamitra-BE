using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceFeedbackConfiguration : IEntityTypeConfiguration<PerformanceFeedback>
{
	public void Configure(EntityTypeBuilder<PerformanceFeedback> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceFeedback", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.ProviderName).HasMaxLength(160);
		builder.Property(x => x.FeedbackText).IsRequired().HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.Source });
	}
}
