using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

public class LeaveYearEndSettlementItemConfiguration : IEntityTypeConfiguration<LeaveYearEndSettlementItem>
{
	public void Configure(EntityTypeBuilder<LeaveYearEndSettlementItem> builder)
	{
		builder.ToTable("HR_LeaveYearEndSettlementItems");
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);

		builder.Property(x => x.PayrollMonth).HasMaxLength(7);
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.IssueCodesJson).HasColumnType("jsonb");
		builder.Property(x => x.AccruedDays).HasPrecision(18, 2);
		builder.Property(x => x.CarryInDays).HasPrecision(18, 2);
		builder.Property(x => x.ApprovedUsedDays).HasPrecision(18, 2);
		builder.Property(x => x.PendingDays).HasPrecision(18, 2);
		builder.Property(x => x.UnusedDays).HasPrecision(18, 2);
		builder.Property(x => x.CarriedDays).HasPrecision(18, 2);
		builder.Property(x => x.EncashedDays).HasPrecision(18, 2);
		builder.Property(x => x.ForfeitedDays).HasPrecision(18, 2);
		builder.Property(x => x.Amount).HasPrecision(18, 2);

		builder.HasIndex(x => new { x.TenantId, x.RunId, x.EmployeeId, x.SourceLeavePolicyRuleId })
			.IsUnique()
			.HasDatabaseName("IX_HR_LeaveYearEndItems_Run_Employee_SourceRule");

		builder.HasIndex(x => new { x.TenantId, x.TargetAcademicYearId, x.EmployeeId, x.TargetLeavePolicyRuleId, x.Status })
			.HasDatabaseName("IX_HR_LeaveYearEndItems_CarryIn");

		builder.HasIndex(x => new { x.TenantId, x.PayrollMonth, x.EmployeeId })
			.HasDatabaseName("IX_HR_LeaveYearEndItems_Payroll");
	}
}
