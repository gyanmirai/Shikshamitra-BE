using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Settings;

public class ProvidentFund : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get;  private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public float EmployeePercentage { get; private set; }
	public float EmployerPercentage { get; private set; }

	protected ProvidentFund() { } // For ORM

	internal ProvidentFund(Guid id, Guid? tenantId,string code, string name, float employeePercentage, float employerPercentage) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		Code = code;
		Name = name;
		EmployeePercentage = employeePercentage;
		EmployerPercentage = employerPercentage;
	}
}
