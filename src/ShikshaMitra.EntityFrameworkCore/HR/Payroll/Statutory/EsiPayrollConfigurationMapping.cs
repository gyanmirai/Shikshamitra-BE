using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiPayrollConfigurationMapping : IEntityTypeConfiguration<EsiPayrollConfiguration>
{
	public void Configure(EntityTypeBuilder<EsiPayrollConfiguration> builder)
	{
		builder.ToTable("AppEsiPayrollConfigurations");
		builder.ConfigureByConvention();
		builder.Property(x => x.EffectiveMonth).HasColumnType("date");
		builder.Property(x => x.CoveredSince).HasColumnType("date");
		builder.Property(x => x.RegistrationNumber).HasMaxLength(100);
		builder.Property(x => x.CancellationReason).HasMaxLength(500);
		builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.EffectiveMonth }).IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"IsCancelled\" = false");
		builder.HasIndex(x => new { x.TenantId, x.RequestFingerprint }).IsUnique();
	}
}
