using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class DeductionComponent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }

	public string Name { get; protected set; } = string.Empty;

	public string? Description { get; protected set; }

	public DeductionComponentType ComponentType { get; protected set; }

	public CalculationType CalculationType { get; protected set; }

	public decimal? Amount { get; protected set; }

	public decimal? Percentage { get; protected set; }

	public Guid? SelectedDeductionComponentId { get; protected set; }

	public ComponentDependency DependOn { get; protected set; }

	public bool VisibleInPayslip { get; protected set; }

	public int DisplayOrder { get; protected set; }

	public bool IsActive { get; protected set; }

	protected DeductionComponent()
	{
	}

	public DeductionComponent(
		Guid id,
		Guid? tenantId,
		string name,
		string? description,
		DeductionComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedDeductionComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder = 0) : base(id)
	{
		TenantId = tenantId;
		SetName(name);
		Description = description;
		ComponentType = componentType;
		SetCalculationFields(componentType, calculationType, amount, percentage, selectedDeductionComponentId);
		DependOn = dependOn;
		VisibleInPayslip = visibleInPayslip;
		DisplayOrder = displayOrder;
		IsActive = true;
	}

	public void Update(
		string name,
		string? description,
		DeductionComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedDeductionComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder)
	{
		SetName(name);
		Description = description;
		ComponentType = componentType;
		SetCalculationFields(componentType, calculationType, amount, percentage, selectedDeductionComponentId);
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
		Check.Length(name, nameof(name), DeductionComponentConsts.MaxNameLength);
		Name = name;
	}

	private void SetCalculationFields(
		DeductionComponentType componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedDeductionComponentId)
	{
		CalculationType = calculationType;

		if (calculationType == CalculationType.Amount)
		{
			if (componentType == DeductionComponentType.Fixed && !amount.HasValue)
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentInvalidAmount)
					.WithData("message", "Amount is required when deductionType is FIXED and calculationType is AMOUNT.");
			}

			if (amount is < 0)
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentInvalidAmount)
					.WithData("message", "Amount must be a non-negative number.");
			}

			Amount = amount;
			Percentage = null;
			SelectedDeductionComponentId = null;
		}
		else
		{
			if (!percentage.HasValue || percentage.Value < 0 || percentage.Value > 100)
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentInvalidPercentage)
					.WithData("message", "Percentage must be between 0 and 100.");
			}

			if (!selectedDeductionComponentId.HasValue)
			{
				throw new BusinessException(HRErrorCodes.PayrollComponentMissingSelectedComponent)
					.WithData("message", "selectedDeductionComponentId is required when calculationType is PERCENTAGE.");
			}

			Percentage = percentage.Value;
			Amount = null;
			SelectedDeductionComponentId = selectedDeductionComponentId;
		}
	}
}
