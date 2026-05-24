using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;

public class ProfessionalReviewCycleConfiguration : IEntityTypeConfiguration<ProfessionalReviewCycle>
{
	public void Configure(EntityTypeBuilder<ProfessionalReviewCycle> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ProfessionalReviewCycles", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.HasIndex(x => new { x.TenantId, x.Status });
	}
}
