using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Persisted monthly payroll preview row for one employee.
/// Used as the source-of-truth for run-payroll screen continuity.
/// </summary>
public class PayrollRunItemSnapshot : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string MonthYear { get; private set; } = string.Empty;
	public Guid EmployeeId { get; private set; }
	public bool IsLocked { get; set; }
	public string ReviewStatus { get; set; } = "PENDING";
	public Guid? ReviewedByUserId { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public bool IsTeaching { get; set; }
	public decimal BasicSalary { get; set; }
	public decimal GrossSalary { get; set; }
	public decimal FullGrossSalary { get; set; }
	public decimal TotalDeduction { get; set; }
	public decimal NetPay { get; set; }
	public decimal? PreviousMonthNetPay { get; set; }
	public decimal? NetPayDelta { get; set; }
	public decimal? NetPayDeltaPercent { get; set; }
	public string IssueCodesJson { get; set; } = "[]";
	public decimal PresentDays { get; set; }
	public decimal HalfWorkingDays { get; set; }
	public decimal TotalWorkingDays { get; set; }
	public decimal PayableDays { get; set; }
	public decimal ProrationFactor { get; set; }
	public string DayCalculationMode { get; set; } = string.Empty;
	public bool UsedDefaultDayCalculation { get; set; }
	public string EarningComponentsJson { get; set; } = "[]";
	public string DeductionComponentsJson { get; set; } = "[]";

	private PayrollRunItemSnapshot()
	{
	}

	public PayrollRunItemSnapshot(
		Guid id,
		Guid? tenantId,
		string monthYear,
		Guid employeeId) : base(id)
	{
		TenantId = tenantId;
		MonthYear = monthYear;
		EmployeeId = employeeId;
	}

	public void MarkReviewed(Guid reviewedByUserId, DateTime reviewedAt)
	{
		ReviewStatus = "REVIEWED";
		ReviewedByUserId = reviewedByUserId;
		ReviewedAt = reviewedAt;
	}

	public void MarkPending()
	{
		if (IsLocked)
		{
			return;
		}

		ReviewStatus = "PENDING";
		ReviewedByUserId = null;
		ReviewedAt = null;
	}

	public void MarkLocked()
	{
		IsLocked = true;
		ReviewStatus = "LOCKED";
	}
}
