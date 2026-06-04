using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class EarningComponent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }

	public string Name { get; protected set; } = string.Empty;

	public string? Description { get; protected set; }

	public EarningComponentType ComponentType { get; protected set; }

	public CalculationType CalculationType { get; protected set; }

	public decimal? Amount { get; protected set; }

	public decimal? Percentage { get; protected set; }

	public Guid? SelectedEarningComponentId { get; protected set; }

	public ComponentDependency DependOn { get; protected set; }

	public bool VisibleInPayslip { get; protected set; }

	public int DisplayOrder { get; protected set; }

	public bool IsActive { get; protected set; }

	protected EarningComponent()
	{
	}

	public EarningComponent(
		Guid id,
		Guid? tenantId,
		string name,
		string? description,
		EarningComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedEarningComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder = 0) : base(id)
	{
		TenantId = tenantId;
		SetName(name);
		Description = description;
		ComponentType = componentType;
		SetCalculationFields(calculationType, amount, percentage, selectedEarningComponentId);
		DependOn = dependOn;
		VisibleInPayslip = visibleInPayslip;
		DisplayOrder = displayOrder;
		IsActive = true;
	}

	public void Update(
		string name,
		string? description,
		EarningComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedEarningComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder)
	{
		SetName(name);
		Description = description;
		ComponentType = componentType;
		SetCalculationFields(calculationType, amount, percentage, selectedEarningComponentId);
		DependOn = dependOn;
		VisibleInPayslip = visibleInPayslip;
		DisplayOrder = displayOrder;
	}

	public void Activate() => IsActive = true;

	public void Deactivate() => IsActive = false;

	private void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		name = name.Trim();
		Check.Length(name, nameof(name), EarningComponentConsts.MaxNameLength);
		Name = name;
	}

	private void SetCalculationFields(
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedEarningComponentId)
	{
		CalculationType = calculationType;

		if (calculationType == CalculationType.Amount)
		{
			if (!amount.HasValue || amount.Value < 0)
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentInvalidAmount)
					.WithData("message", "Amount must be a non-negative number when calculationType is AMOUNT.");
			}

			Amount = amount.Value;
			Percentage = null;
			SelectedEarningComponentId = null;
		}
		else
		{
			if (!percentage.HasValue || percentage.Value < 0 || percentage.Value > 100)
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentInvalidPercentage)
					.WithData("message", "Percentage must be between 0 and 100.");
			}

			if (!selectedEarningComponentId.HasValue)
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentMissingSelectedComponent)
					.WithData("message", "selectedEarningComponentId is required when calculationType is PERCENTAGE.");
			}

			Percentage = percentage.Value;
			Amount = null;
			SelectedEarningComponentId = selectedEarningComponentId;
		}
	}
}
