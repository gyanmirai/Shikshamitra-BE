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
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		ArgumentNullException.ThrowIfNull(sourceScope);
		if (string.IsNullOrWhiteSpace(eventKey) || eventKey.Length > 160) throw new BusinessException(NotificationErrorCodes.EventUnknown);
		var ancestors = new List<string> { NotificationRuleScope.Create().Key };
		for (var depth = 1; depth <= sourceScope.Depth; depth++)
			ancestors.Add(NotificationRuleScope.Create(sourceScope.AcademicYearId, depth >= 2 ? sourceScope.BoardId : null,
				depth >= 3 ? sourceScope.MediumId : null, depth >= 4 ? sourceScope.ClassId : null, depth >= 5 ? sourceScope.DivisionId : null).Key);
		var keys = ancestors.ToArray();
		var rows = await executer.ToListAsync((await rules.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.EventKey == eventKey && Enumerable.Contains(keys, x.ScopeKey)), cancellationToken);
		if (rows.GroupBy(x => x.ScopeKey, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new BusinessException(NotificationErrorCodes.StateConflict);
		return rows.OrderByDescending(x => Array.IndexOf(keys, x.ScopeKey)).FirstOrDefault();
	}
}
