using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Represents a pay element in the payroll system.
/// </summary>
public class PayElement : AuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// TenantId of the pay element.
	/// </summary>
	public Guid? TenantId { get; }

	/// <summary>
	/// Unique code for the pay element.
	/// </summary>
	public string Code { get; set; }

	/// <summary>
	/// Name of the pay element.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Type of the pay element (e.g., Allowance, Deduction).
	/// </summary>
	public PayElementType Type { get; }

	/// <summary>
	/// Indicates whether the pay element is specific to an employee.
	/// </summary>
	public bool IsEmployeeSpecific { get; set; }

	/// <summary>
	/// Indicates whether the pay element is dependent on another element.
	/// </summary>
	public bool IsDependent { get; set; }

	/// <summary>
	/// Dependent properties, applicable only if IsDependent is true.
	/// </summary>
	public DependentProperties? Dependent { get; set; }

	/// <summary>
	/// Indicates whether the pay element is visible in the pay slip.
	/// </summary>
	public bool IsVisibleInPaySlip { get; set; }

	// For ORM
	protected PayElement()
	{
	}

	/// <summary>
	/// Initializes a new instance of the PayElement class.
	/// </summary>
	/// <param name="id">Unique identifier of the pay element.</param>
	/// <param name="tenantId">TenantId of the pay element.</param>
	/// <param name="code">Unique code for the pay element.</param>
	/// <param name="name">Name of the pay element.</param>
	/// <param name="type">Type of the pay element.</param>
	/// <param name="isEmployeeSpecific">Indicates whether the pay element is specific to an employee.</param>
	/// <param name="isDependent">Indicates whether the pay element is dependent on another element.</param>
	/// <param name="dependent">Dependent properties, applicable only if IsDependent is true.</param>
	/// <param name="isVisibleInPaySlip">Indicates whether the pay element is visible in the pay slip.</param>
	public PayElement(Guid id, Guid? tenantId, string code, string name, PayElementType type, bool isEmployeeSpecific,
		bool isDependent, DependentProperties? dependent, bool isVisibleInPaySlip) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		Code = code ?? throw new ArgumentNullException(nameof(code));
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Type = type;
		IsEmployeeSpecific = isEmployeeSpecific;
		IsDependent = isDependent;
		Dependent = dependent;
		IsVisibleInPaySlip = isVisibleInPaySlip;
	}


	public class DependentProperties
	{
		/// <summary>
		/// Code of the dependent element.
		/// </summary>
		public string ElementId { get; set; } = string.Empty;

		private float? percentage;
		private float? amount;

		/// <summary>
		/// Percentage of the dependent element. Only one of Percentage or Amount can be set.
		/// </summary>
		public float? Percentage
		{
			get => percentage;
			init
			{
				if (value.HasValue && Amount.HasValue)
				{
					throw new InvalidOperationException("Only one of Percentage or Amount can be set.");
				}

				percentage = value;
			}
		}

		/// <summary>
		/// Fixed amount of the dependent element. Only one of Percentage or Amount can be set.
		/// </summary>
		public float? Amount
		{
			get => amount;
			init
			{
				if (value.HasValue && Percentage.HasValue)
				{
					throw new InvalidOperationException("Only one of Percentage or Amount can be set.");
				}

				amount = value;
			}
		}
	}
}