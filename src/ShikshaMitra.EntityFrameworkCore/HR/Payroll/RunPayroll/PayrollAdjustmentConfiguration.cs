using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollAdjustmentConfiguration : IEntityTypeConfiguration<PayrollAdjustment>
{
	public void Configure(EntityTypeBuilder<PayrollAdjustment> builder)
	{
		builder.ToTable("HR_PayrollAdjustments");

		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.MonthYear).IsRequired().HasMaxLength(7);
		builder.Property(x => x.EmployeeId).IsRequired();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(512);
		builder.Property(x => x.Description).HasMaxLength(1024);
		builder.Property(x => x.SourceType).HasMaxLength(64);
		builder.Property(x => x.IsSystemGenerated).IsRequired().HasDefaultValue(false);
		builder.Property(x => x.Amount).HasPrecision(18, 2);

		builder.HasIndex(x => new { x.TenantId, x.MonthYear, x.EmployeeId })
			.HasDatabaseName("IX_HR_PayrollAdjustments_Tenant_Month_Employee");

		builder.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId })
			.IsUnique()
			.HasDatabaseName("IX_HR_PayrollAdjustments_Tenant_Source");
	}
}
