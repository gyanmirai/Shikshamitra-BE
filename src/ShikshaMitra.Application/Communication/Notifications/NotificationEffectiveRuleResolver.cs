using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationEffectiveRuleResolver(ICurrentTenant tenant, IRepository<NotificationRule, Guid> rules,
	IAsyncQueryableExecuter executer) : ITransientDependency
{
	public async Task<NotificationRule?> ResolveAsync(string eventKey, NotificationRuleScope sourceScope, CancellationToken cancellationToken = default)
		=> (await ResolveManyAsync([eventKey], sourceScope, cancellationToken)).GetValueOrDefault(eventKey);

	internal async Task<IReadOnlyDictionary<string, NotificationRule>> ResolveManyAsync(string[] eventKeys,
		NotificationRuleScope sourceScope, CancellationToken cancellationToken) =>
		(await GetApplicableAsync(eventKeys, sourceScope, cancellationToken))
			.GroupBy(x => x.EventKey, StringComparer.Ordinal)
			.ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

	internal async Task<bool> IsEnabledForIntentAsync(NotificationIntent intent, NotificationRuleScope sourceScope,
		CancellationToken cancellationToken)
	{
		if (!tenant.Id.HasValue || tenant.Id != intent.TenantId) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var applicable = await GetApplicableAsync([intent.EventKey], sourceScope, cancellationToken);
		return applicable.FirstOrDefault() is { IsEnabled: true } &&
			applicable.Any(x => x.Id == intent.RuleId && x.IsEnabled && x.Revision >= intent.RuleRevision);
	}

	private async Task<IReadOnlyList<NotificationRule>> GetApplicableAsync(string[] eventKeys, NotificationRuleScope sourceScope,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		ArgumentNullException.ThrowIfNull(sourceScope);
		if (eventKeys.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 160)) throw new BusinessException(NotificationErrorCodes.EventUnknown);
		if (eventKeys.Length == 0) return [];
		var ancestors = new List<string> { NotificationRuleScope.Create().Key };
		for (var depth = 1; depth <= sourceScope.Depth; depth++)
			ancestors.Add(NotificationRuleScope.Create(sourceScope.AcademicYearId, depth >= 2 ? sourceScope.BoardId : null,
				depth >= 3 ? sourceScope.MediumId : null, depth >= 4 ? sourceScope.ClassId : null, depth >= 5 ? sourceScope.DivisionId : null).Key);
		var keys = ancestors.ToArray();
		using var tracking = rules.DisableTracking();
		var rows = await executer.ToListAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			Enumerable.Contains(eventKeys, x.EventKey) && Enumerable.Contains(keys, x.ScopeKey)), cancellationToken);
		if (rows.GroupBy(x => (x.EventKey, x.ScopeKey)).Any(x => x.Count() > 1)) throw new BusinessException(NotificationErrorCodes.StateConflict);
		return rows.OrderByDescending(x => Array.IndexOf(keys, x.ScopeKey)).ToArray();
	}
}
