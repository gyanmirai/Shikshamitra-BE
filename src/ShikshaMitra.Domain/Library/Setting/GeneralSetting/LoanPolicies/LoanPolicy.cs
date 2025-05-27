using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LoanPolicies;

public class LoanPolicy : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid RoleId { get; set; }
	public int LoanDurationInDays { get; set; }
	public int MaxBooksAllowed { get; set; }
	public decimal LateReturnFinePerDay { get; set; }

	private LoanPolicy() { }
    public LoanPolicy(Guid id, Guid roleId, int loanDurationInDays, int maxBooksAllowed, decimal lateReturnFinePerDay, Guid? tenantId)
		: base(id)
	{
		RoleId = roleId;
		LoanDurationInDays = loanDurationInDays;
		MaxBooksAllowed = maxBooksAllowed;
		LateReturnFinePerDay = lateReturnFinePerDay;
		TenantId = tenantId;
	}
}