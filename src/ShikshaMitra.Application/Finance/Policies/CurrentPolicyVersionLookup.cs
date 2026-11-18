using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Domain.Finance.Policies;

internal static class CurrentPolicyVersionLookup
{
	public static async Task PopulateAsync<T>(IRepository<FinancePolicyVersion, Guid> repository, string family, IEnumerable<T> values,
		IAsyncQueryableExecuter asyncExecuter)
		where T : ICurrentPolicyDto
	{
		var items = values.ToList();
		foreach (var item in items) item.CurrentVersionId = null;
		var active = items.Where(x => x.Status == "Active").ToList();
		if (active.Count == 0) return;

		// Match exact policy/year/version tuples in one query; historical snapshots
		// and versions belonging to inactive policies never need to leave the DB.
		var parameter = Expression.Parameter(typeof(FinancePolicyVersion), "version");
		Expression matches = Expression.Constant(false);
		foreach (var item in active)
		{
			var tuple = Expression.AndAlso(
				Expression.Equal(Expression.Property(parameter, nameof(FinancePolicyVersion.PolicyId)), Expression.Constant(item.Id)),
				Expression.AndAlso(
					Expression.Equal(Expression.Property(parameter, nameof(FinancePolicyVersion.AcademicYearId)), Expression.Constant(item.AcademicYearId)),
					Expression.Equal(Expression.Property(parameter, nameof(FinancePolicyVersion.VersionNumber)), Expression.Constant(item.Version))));
			matches = Expression.OrElse(matches, tuple);
		}
		var predicate = Expression.Lambda<Func<FinancePolicyVersion, bool>>(matches, parameter);
		var query = (await repository.GetQueryableAsync())
			.Where(x => x.PolicyType == family && x.Status == "Active").Where(predicate);
		var versions = await asyncExecuter.ToListAsync(query.Select(x => new { x.Id, x.PolicyId, x.AcademicYearId, x.VersionNumber }));
		var byVersion = versions.ToDictionary(x => (x.PolicyId, x.AcademicYearId, x.VersionNumber), x => x.Id);
		foreach (var item in items)
			if (byVersion.TryGetValue((item.Id, item.AcademicYearId, item.Version), out var id)) item.CurrentVersionId = id;
	}
}
