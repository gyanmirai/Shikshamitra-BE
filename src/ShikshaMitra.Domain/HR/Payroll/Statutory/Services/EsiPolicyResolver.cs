using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Statutory;

public sealed record EsiPolicyResolution(EsiPolicy Policy, StatutoryRuleVersion Rule);

public class EsiPolicyResolver(IRepository<EsiPolicy, Guid> policies,
	IRepository<StatutoryRuleVersion, Guid> rules) : ITransientDependency
{
	public async Task<IReadOnlyDictionary<Guid, EsiPolicyResolution>> ResolveAllAsync(DateTime month)
	{
		var date = new DateTime(month.Year, month.Month, 1);
		var monthEnd = date.AddMonths(1).AddDays(-1);
		var policyList = await policies.GetListAsync();
		var versions = await rules.GetListAsync(x => x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.IsActive && x.EsiPolicyId != null && x.EffectiveFrom <= monthEnd);
		var result = new Dictionary<Guid, EsiPolicyResolution>();
		foreach (var policy in policyList)
		{
			// The next immutable version starts a new interval. Never fall back through a gap.
			var version = versions.Where(x => x.EsiPolicyId == policy.Id)
				.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).FirstOrDefault();
			if (version != null && (!version.EffectiveTo.HasValue || version.EffectiveTo.Value >= date))
				result[policy.Id] = new(policy, version);
		}
		return result;
	}
	public async Task<EsiPolicyResolution> ResolveAsync(Guid? policyId, DateTime month, bool allowArchived = true)
	{
		var resolved = await ResolveAllAsync(month);
		var selected = policyId.HasValue ? resolved.GetValueOrDefault(policyId.Value)
			: resolved.Values.SingleOrDefault(x => x.Policy.IsDefault);
		if (selected == null) throw NotEffective(policyId, month);
		if (!allowArchived && selected.Policy.IsArchived) throw new BusinessException(HRErrorCodes.EsiPolicyArchived);
		return selected;
	}
	public static BusinessException NotEffective(Guid? policyId, DateTime month) =>
		new BusinessException(HRErrorCodes.EsiPolicyNotEffective).WithData("policyId", policyId!)
			.WithData("month", month.ToString("yyyy-MM"));
}
