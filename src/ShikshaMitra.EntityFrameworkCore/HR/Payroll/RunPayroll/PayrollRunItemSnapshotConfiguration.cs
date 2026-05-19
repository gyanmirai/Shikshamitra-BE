using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollRunItemSnapshotConfiguration : IEntityTypeConfiguration<PayrollRunItemSnapshot>
{
	public void Configure(EntityTypeBuilder<PayrollRunItemSnapshot> builder)
	{
		builder.ToTable("HR_PayrollRunItems");

		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.MonthYear).IsRequired().HasMaxLength(7);
		builder.Property(x => x.EmployeeName).IsRequired().HasMaxLength(256);
		builder.Property(x => x.EmployeeIdentifier).IsRequired().HasMaxLength(64);
		builder.Property(x => x.ReviewStatus).IsRequired().HasMaxLength(16);
		builder.Property(x => x.DayCalculationMode).IsRequired().HasMaxLength(64);
		builder.Property(x => x.PayrollAttendanceSource).IsRequired().HasMaxLength(64);

		builder.Property(x => x.BasicSalary).HasPrecision(18, 2);
		builder.Property(x => x.GrossSalary).HasPrecision(18, 2);
		builder.Property(x => x.FullGrossSalary).HasPrecision(18, 2);
		builder.Property(x => x.TotalDeduction).HasPrecision(18, 2);
		builder.Property(x => x.NetPay).HasPrecision(18, 2);
		builder.Property(x => x.PreviousMonthNetPay).HasPrecision(18, 2);
		builder.Property(x => x.NetPayDelta).HasPrecision(18, 2);
		builder.Property(x => x.NetPayDeltaPercent).HasPrecision(18, 4);
		builder.Property(x => x.PresentDays).HasPrecision(18, 2);
		builder.Property(x => x.HalfWorkingDays).HasPrecision(18, 2);
		builder.Property(x => x.TotalWorkingDays).HasPrecision(18, 2);
		builder.Property(x => x.PayableDays).HasPrecision(18, 2);
		builder.Property(x => x.ProrationFactor).HasPrecision(18, 4);

		builder.Property(x => x.IssueCodesJson).HasColumnType("jsonb");
		builder.Property(x => x.AttendancePolicyWarningsJson).HasColumnType("jsonb");
		builder.Property(x => x.EarningComponentsJson).HasColumnType("jsonb");
		builder.Property(x => x.DeductionComponentsJson).HasColumnType("jsonb");

		builder.HasIndex(x => new { x.TenantId, x.MonthYear, x.EmployeeId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false")
			.HasDatabaseName("IX_HR_PayrollRunItems_Tenant_Month_Employee");

		builder.HasIndex(x => new { x.MonthYear, x.ReviewStatus })
			.HasDatabaseName("IX_HR_PayrollRunItems_Month_ReviewStatus");
	}
}
