using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Auditable review marker for one employee in one monthly payroll run.
/// </summary>
public class PayrollRunReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string MonthYear { get; private set; } = string.Empty;
	public Guid EmployeeId { get; private set; }
	public Guid ReviewedByUserId { get; private set; }
	public DateTime ReviewedAt { get; private set; }
	public Guid? PreviewRunId { get; private set; }

	private PayrollRunReview()
	{
	}

	public PayrollRunReview(
		Guid id,
		Guid? tenantId,
		string monthYear,
		Guid employeeId,
		Guid reviewedByUserId,
		DateTime reviewedAt,
		Guid? previewRunId = null) : base(id)
	{
		TenantId = tenantId;
		MonthYear = monthYear;
		EmployeeId = employeeId;
		ReviewedByUserId = reviewedByUserId;
		ReviewedAt = reviewedAt;
		PreviewRunId = previewRunId;
	}

	public void Refresh(Guid reviewedByUserId, DateTime reviewedAt, Guid? previewRunId = null)
	{
		ReviewedByUserId = reviewedByUserId;
		ReviewedAt = reviewedAt;
		PreviewRunId = previewRunId;
	}
}
