using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.Payroll.Settings;

public class PayrollComponentVersionConfiguration : IEntityTypeConfiguration<PayrollComponentVersion>
{
	public void Configure(EntityTypeBuilder<PayrollComponentVersion> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PayrollComponentVersions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(x => x.Id);
		builder.HasIndex(x => new { x.TenantId, x.IsEarning, x.ComponentId, x.EffectiveMonth });

		builder.Property(x => x.EffectiveMonth).IsRequired().HasMaxLength(7);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Description).HasMaxLength(255);
		builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
		builder.Property(x => x.Percentage).HasColumnType("decimal(18,2)");
		builder.Property(x => x.CalculationType).IsRequired();
		builder.Property(x => x.ComponentType).IsRequired();
		builder.Property(x => x.DependOn).IsRequired();
		builder.Property(x => x.VisibleInPayslip).IsRequired();
	}
}
