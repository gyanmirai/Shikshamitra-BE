using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class DevelopmentPlanConfiguration : IEntityTypeConfiguration<DevelopmentPlan>
{
	public void Configure(EntityTypeBuilder<DevelopmentPlan> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "DevelopmentPlans", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Goal).IsRequired().HasMaxLength(240);
		builder.Property(x => x.ActionPlan).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.Status });
	}
}
