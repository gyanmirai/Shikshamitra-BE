using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollAdjustment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string MonthYear { get; private set; } = string.Empty;
	public Guid EmployeeId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public decimal Amount { get; private set; }
	public bool IsEarning { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string? Description { get; private set; }

	private PayrollAdjustment()
	{
	}

	public PayrollAdjustment(
		Guid id,
		Guid? tenantId,
		string monthYear,
		Guid employeeId,
		string name,
		decimal amount,
		bool isEarning,
		string reason,
		string? description = null) : base(id)
	{
		TenantId = tenantId;
		MonthYear = monthYear;
		EmployeeId = employeeId;
		Update(name, amount, isEarning, reason, description);
	}

	public void Update(
		string name,
		decimal amount,
		bool isEarning,
		string reason,
		string? description = null)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new BusinessException(HRErrorCodes.PayrollAdjustmentNameRequired);
		}

		if (amount <= 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollAdjustmentAmountInvalid);
		}

		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException(HRErrorCodes.PayrollAdjustmentReasonRequired);
		}

		Name = name.Trim();
		Amount = amount;
		IsEarning = isEarning;
		Reason = reason.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
	}
}
