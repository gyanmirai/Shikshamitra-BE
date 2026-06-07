using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollMonthLockConfiguration : IEntityTypeConfiguration<PayrollMonthLock>
{
	public void Configure(EntityTypeBuilder<PayrollMonthLock> builder)
	{
		builder.ToTable("HR_PayrollMonthLocks");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.MonthYear)
			.IsRequired()
			.HasMaxLength(7);

		builder.HasIndex(x => new { x.TenantId, x.MonthYear })
			.IsUnique()
			.HasDatabaseName("IX_HR_PayrollMonthLocks_Tenant_Month");

		builder.HasIndex(x => x.ExpiresAt)
			.HasDatabaseName("IX_HR_PayrollMonthLocks_ExpiresAt");
	}
}
