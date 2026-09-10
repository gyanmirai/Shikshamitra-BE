using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Domain.Finance.Policies;

internal static class CurrentPolicyVersionLookup
{
	public static async Task PopulateAsync<T>(IRepository<FinancePolicyVersion, Guid> repository, string family, IEnumerable<T> values)
		where T : ICurrentPolicyDto
	{
		var items = values.ToList();
		var ids = items.Select(x => x.Id).Distinct().ToList();
		if (ids.Count == 0) return;
		var versions = await repository.GetListAsync(x => x.PolicyType == family && ids.Contains(x.PolicyId) && x.Status == "Active");
		foreach (var item in items)
			item.CurrentVersionId = item.Status == "Active"
				? versions.SingleOrDefault(x => x.PolicyId == item.Id && x.AcademicYearId == item.AcademicYearId && x.VersionNumber == item.Version)?.Id
				: null;
	}
}
