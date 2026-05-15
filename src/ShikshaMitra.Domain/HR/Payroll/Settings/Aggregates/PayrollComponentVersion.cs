using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayrollComponentVersion : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid ComponentId { get; protected set; }
	public bool IsEarning { get; protected set; }
	public string EffectiveMonth { get; protected set; } = string.Empty;
	public string Name { get; protected set; } = string.Empty;
	public string? Description { get; protected set; }
	public int ComponentType { get; protected set; }
	public CalculationType CalculationType { get; protected set; }
	public decimal? Amount { get; protected set; }
	public decimal? Percentage { get; protected set; }
	public Guid? SelectedComponentId { get; protected set; }
	public ComponentDependency DependOn { get; protected set; }
	public bool VisibleInPayslip { get; protected set; }
	public int DisplayOrder { get; protected set; }

	protected PayrollComponentVersion()
	{
	}

	private PayrollComponentVersion(
		Guid id,
		Guid? tenantId,
		Guid componentId,
		bool isEarning,
		string effectiveMonth,
		string name,
		string? description,
		int componentType,
		CalculationType calculationType,
		decimal? amount,
		decimal? percentage,
		Guid? selectedComponentId,
		ComponentDependency dependOn,
		bool visibleInPayslip,
		int displayOrder) : base(id)
	{
		TenantId = tenantId;
		ComponentId = componentId;
		IsEarning = isEarning;
		EffectiveMonth = NormalizeMonth(effectiveMonth);
		Name = name;
		Description = description;
		ComponentType = componentType;
		CalculationType = calculationType;
		Amount = amount;
		Percentage = percentage;
		SelectedComponentId = selectedComponentId;
		DependOn = dependOn;
		VisibleInPayslip = visibleInPayslip;
		DisplayOrder = displayOrder;
	}

	public static PayrollComponentVersion FromEarningComponent(Guid id, string effectiveMonth, EarningComponent component)
	{
		return new PayrollComponentVersion(
			id,
			component.TenantId,
			component.Id,
			true,
			effectiveMonth,
			component.Name,
			component.Description,
			(int)component.ComponentType,
			component.CalculationType,
			component.Amount,
			component.Percentage,
			component.SelectedEarningComponentId,
			component.DependOn,
			component.VisibleInPayslip,
			component.DisplayOrder);
	}

	public static PayrollComponentVersion FromDeductionComponent(Guid id, string effectiveMonth, DeductionComponent component)
	{
		return new PayrollComponentVersion(
			id,
			component.TenantId,
			component.Id,
			false,
			effectiveMonth,
			component.Name,
			component.Description,
			(int)component.ComponentType,
			component.CalculationType,
			component.Amount,
			component.Percentage,
			component.SelectedDeductionComponentId,
			component.DependOn,
			component.VisibleInPayslip,
			component.DisplayOrder);
	}

	private static string NormalizeMonth(string month)
	{
		return DateTime.TryParse($"{month}-01", out var parsed)
			? parsed.ToString("yyyy-MM")
			: month;
	}
}
