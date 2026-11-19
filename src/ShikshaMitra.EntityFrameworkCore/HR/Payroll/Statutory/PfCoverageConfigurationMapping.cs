using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class PfCoverageConfigurationMapping : IEntityTypeConfiguration<PfCoverageConfiguration>
{
	public void Configure(EntityTypeBuilder<PfCoverageConfiguration> builder)
	{
		builder.ToTable("AppPfCoverageConfigurations");
		builder.ConfigureByConvention();
		builder.Property(x => x.EffectiveMonth).HasColumnType("date");
		builder.Property(x => x.EvidenceReference).HasMaxLength(1000).IsRequired();
		builder.Property(x => x.RequestKey).HasMaxLength(32).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.EffectiveMonth }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"IsCancelled\" = false");
		builder.HasIndex(x => new { x.TenantId, x.RequestKey }).IsUnique();
	}
}
