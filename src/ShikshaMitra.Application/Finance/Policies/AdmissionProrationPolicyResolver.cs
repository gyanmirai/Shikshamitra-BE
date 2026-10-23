using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Policies;

public class AdmissionProrationPolicyResolver(IRepository<ProrationPolicy, Guid> policies,
	IRepository<FinancePolicyVersion, Guid> versions, ICurrentTenant tenant) : ITransientDependency
{
	public async Task<FinancePolicyVersion?> ResolveAsync(Guid year)
	{
		var defaults = await policies.GetListAsync(x => x.TenantId == tenant.Id && x.AcademicYearId == year && x.IsDefault && x.Status == ProrationPolicyStatus.Active);
		if (defaults.Count == 0) return null;
		if (defaults.Count != 1) throw new BusinessException(ProrationPolicyErrorCodes.PolicyAmbiguous);
		var policy = defaults[0];
		return await versions.FindAsync(x => x.TenantId == tenant.Id && x.AcademicYearId == year && x.PolicyId == policy.Id && x.PolicyType == "proration" && x.VersionNumber == policy.Version && x.Status == "Active")
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionMissing);
	}
}
