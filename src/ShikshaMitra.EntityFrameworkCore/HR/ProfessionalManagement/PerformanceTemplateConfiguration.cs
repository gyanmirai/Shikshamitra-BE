using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceTemplateConfiguration : IEntityTypeConfiguration<PerformanceTemplate>
{
	public void Configure(EntityTypeBuilder<PerformanceTemplate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceTemplates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.GujaratiName).HasMaxLength(160);
		builder.Property(x => x.RoleName).HasMaxLength(120);
		builder.Property(x => x.Description).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.BoardType, x.StaffCategory, x.Status });
	}
}
