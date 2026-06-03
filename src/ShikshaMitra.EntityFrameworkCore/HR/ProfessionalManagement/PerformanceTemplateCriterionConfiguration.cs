using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceTemplateCriterionConfiguration : IEntityTypeConfiguration<PerformanceTemplateCriterion>
{
	public void Configure(EntityTypeBuilder<PerformanceTemplateCriterion> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceTemplateCriteria", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).HasMaxLength(1000);
		builder.Property(x => x.Weight).HasPrecision(7, 2);
		builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.SectionId, x.DisplayOrder });
	}
}
