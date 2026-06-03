using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceEvidenceConfiguration : IEntityTypeConfiguration<PerformanceEvidence>
{
	public void Configure(EntityTypeBuilder<PerformanceEvidence> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceEvidence", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
		builder.Property(x => x.FileName).HasMaxLength(260);
		builder.Property(x => x.Url).HasMaxLength(1000);
		builder.Property(x => x.Notes).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId });
	}
}
