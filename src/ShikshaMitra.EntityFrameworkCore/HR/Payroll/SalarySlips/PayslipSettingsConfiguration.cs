using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipSettingsConfiguration : IEntityTypeConfiguration<PayslipSettings>
{
	public void Configure(EntityTypeBuilder<PayslipSettings> builder)
	{
		builder.ToTable("HR_PayslipSettings");
		builder.ConfigureByConvention();
		builder.Property(x => x.SchoolName).IsRequired().HasMaxLength(250);
		builder.Property(x => x.SchoolAddress).IsRequired().HasMaxLength(1000);
		builder.HasIndex(x => x.TenantId).IsUnique();
	}
}
