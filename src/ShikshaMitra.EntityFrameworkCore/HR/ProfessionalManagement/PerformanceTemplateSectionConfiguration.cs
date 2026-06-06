using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceTemplateSectionConfiguration : IEntityTypeConfiguration<PerformanceTemplateSection>
{
	public void Configure(EntityTypeBuilder<PerformanceTemplateSection> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceTemplateSections", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.GujaratiName).HasMaxLength(160);
		builder.Property(x => x.Weight).HasPrecision(7, 2);
		builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.DisplayOrder });
	}
}
