using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollInputRevision : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid Revision { get; private set; }
	private PayrollInputRevision() { }
	public PayrollInputRevision(Guid? tenantId) : base(tenantId ?? Guid.Empty) { TenantId = tenantId; }
}

public interface IPayrollInputRevisionStore
{
	Task<Guid> ReadAsync();
	// Held until the owning transaction commits. Source writes acquire the same fence.
	Task<Guid> LockAsync();
}
