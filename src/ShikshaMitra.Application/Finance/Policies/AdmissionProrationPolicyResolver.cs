using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Policies;

/// <summary>Shared academic-year default for Admission preparation and Finance structure assignment.</summary>
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

	public async Task<List<FinancePolicyVersion>> GetActiveVersionsAsync(Guid year)
	{
		var active = await policies.GetListAsync(x => x.TenantId == tenant.Id && x.AcademicYearId == year && x.Status == ProrationPolicyStatus.Active);
		var result = new List<FinancePolicyVersion>();
		foreach (var policy in active)
		{
			var version = await versions.FindAsync(x => x.TenantId == tenant.Id && x.AcademicYearId == year &&
				x.PolicyId == policy.Id && x.PolicyType == "proration" && x.VersionNumber == policy.Version && x.Status == "Active");
			if (version != null) result.Add(version);
		}
		return result.OrderBy(x => x.PolicyName).ToList();
	}

	public async Task<FinancePolicyVersion> ResolveActiveVersionAsync(Guid year, Guid versionId)
	{
		var version = await versions.FindAsync(x => x.TenantId == tenant.Id && x.Id == versionId &&
			x.AcademicYearId == year && x.PolicyType == "proration" && x.Status == "Active")
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionMissing);
		var policy = await policies.FindAsync(x => x.TenantId == tenant.Id && x.Id == version.PolicyId &&
			x.AcademicYearId == year && x.Status == ProrationPolicyStatus.Active);
		if (policy == null || policy.Version != version.VersionNumber)
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionMissing);
		return version;
	}
}
