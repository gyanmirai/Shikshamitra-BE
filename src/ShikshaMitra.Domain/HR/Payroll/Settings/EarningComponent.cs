using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Represents an earning component master data in the payroll system.
/// This is used as a template for defining salary components like Basic Salary, HRA, etc.
/// </summary>
public class EarningComponent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	
	/// <summary>
	/// Unique name of the earning component (e.g., "Basic Salary", "House Rent Allowance")
	/// </summary>
	public string Name { get; protected set; } = string.Empty;
	
	/// <summary>
	/// Detailed description of the component
	/// </summary>
	public string? Description { get; protected set; }
	
	/// <summary>
	/// Type of component (Fixed or Variable)
	/// </summary>
	public EarningComponentType ComponentType { get; protected set; }
	
	/// <summary>
	/// What this component depends on for calculation
	/// </summary>
	public ComponentDependency DependOn { get; protected set; }
	
	/// <summary>
	/// Amount value - can be fixed amount (e.g., "30000") or percentage (e.g., "40%")
	/// Stored as string to support both "30000" and "40%" formats
	/// </summary>
	public string Amount { get; protected set; } = string.Empty;
	
	/// <summary>
	/// Whether this component should be visible in the employee payslip
	/// </summary>
	public bool VisibleInPayslip { get; protected set; }
	
	/// <summary>
	/// Display order for sorting
	/// </summary>
	public int DisplayOrder { get; protected set; }
	
	/// <summary>
	/// Whether this component is active
	/// </summary>
	public bool IsActive { get; protected set; }

	// For EF Core
	protected EarningComponent()
	{
	}

	public EarningComponent(
		Guid id,
		Guid? tenantId,
		string name,
		string? description,
		EarningComponentType componentType,
		ComponentDependency dependOn,
		string amount,
		bool visibleInPayslip,
		int displayOrder = 0) : base(id)
	{
		TenantId = tenantId;
		SetName(name);
		Description = description;
		ComponentType = componentType;
		DependOn = dependOn;
		SetAmount(amount, componentType, dependOn);
		VisibleInPayslip = visibleInPayslip;
		DisplayOrder = displayOrder;
		IsActive = true;
	}

	public void Update(
		string name,
		string? description,
		EarningComponentType componentType,
		ComponentDependency dependOn,
		string amount,
		bool visibleInPayslip,
		int displayOrder)
	{
		SetName(name);
		Description = description;
		ComponentType = componentType;
		DependOn = dependOn;
		SetAmount(amount, componentType, dependOn);
		VisibleInPayslip = visibleInPayslip;
		DisplayOrder = displayOrder;
	}

	public void Activate()
	{
		IsActive = true;
	}

	public void Deactivate()
	{
		IsActive = false;
	}

	private void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Check.Length(name, nameof(name), EarningComponentConsts.MaxNameLength);
		Name = name;
	}

	private void SetAmount(string amount, EarningComponentType componentType, ComponentDependency dependOn)
	{
		Check.NotNullOrWhiteSpace(amount, nameof(amount));
		Check.Length(amount, nameof(amount), EarningComponentConsts.MaxAmountLength);
		
		var amountTrimmed = amount.Trim();
		
		// Validate amount format based on dependency type
		if (dependOn == ComponentDependency.None)
		{
			// For components with no dependency (regardless of type), amount must be numeric
			if (!decimal.TryParse(amountTrimmed, out var numericAmount) || numericAmount < 0)
			{
				throw new BusinessException("ShikshaMitra:InvalidAmount")
					.WithData("message", "Amount must be a valid non-negative number for components with no dependency.");
			}
			Amount = amountTrimmed;
		}
		else
		{
			// For dependent components, amount can be either numeric or percentage
			if (amountTrimmed.EndsWith("%"))
			{
				var percentageStr = amountTrimmed.TrimEnd('%');
				if (!decimal.TryParse(percentageStr, out var percentage) || percentage < 0 || percentage > 100)
				{
					throw new BusinessException("ShikshaMitra:InvalidPercentage")
						.WithData("message", "Percentage must be between 0 and 100.");
				}
			}
			else
			{
				if (!decimal.TryParse(amountTrimmed, out var numericAmount) || numericAmount < 0)
				{
					throw new BusinessException("ShikshaMitra:InvalidAmount")
						.WithData("message", "Amount must be a valid non-negative number or percentage.");
				}
			}
			Amount = amount;
		}
	}
}

