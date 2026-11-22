using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.Notifications;

public sealed partial class AcademicNotificationOperationAccess
{
	internal sealed record RecentKey(DateTimeOffset CreatedAt, Guid OperationId);
	private sealed record RecentCandidate(Guid OperationId, string EventKey, string SourceType, Guid SourceId, DateTime CreationTime);

	internal async Task<(IReadOnlyList<AcademicNotificationRecentDeliveryDto> Items, RecentKey? Next)> ReadRecentAsync(
		Guid tenantId, UserScopeContext callerScope, string? eventKey, RecentKey? before, CancellationToken cancellationToken)
	{
		const int pageSize = 15;
		const int scanSize = 40;
		var allIntents = (await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);
		var candidates = (from operation in await OwnedOperationsAsync(tenantId)
			join intent in allIntents on (Guid?)operation.Id equals intent.CampaignId
			where operation.SourceEntityId == intent.SourceId && operation.SourceEntityType == intent.SourceType &&
				intent.SourceType != NotificationCampaignTypes.AcademicTemplateTestSource &&
				(eventKey == null || intent.EventKey == eventKey)
			select new { OperationId = operation.Id, intent.EventKey, intent.SourceType, intent.SourceId, operation.CreationTime }).Distinct();
		if (before != null)
		{
			var createdAt = before.CreatedAt.UtcDateTime;
			candidates = candidates.Where(x => x.CreationTime < createdAt ||
				x.CreationTime == createdAt && x.OperationId.CompareTo(before.OperationId) < 0);
		}
		var batch = await queries.ToListAsync(candidates.OrderByDescending(x => x.CreationTime)
			.ThenByDescending(x => x.OperationId).Take(scanSize), cancellationToken);
		var visible = new List<AcademicNotificationRecentDeliveryDto>(pageSize + 1);
		foreach (var candidate in batch)
		{
			var row = new RecentCandidate(candidate.OperationId, candidate.EventKey, candidate.SourceType,
				candidate.SourceId, candidate.CreationTime);
			var item = await ReadRecentItemAsync(row, tenantId, callerScope, cancellationToken);
			if (item != null) visible.Add(item);
			if (visible.Count > pageSize) break;
		}
		if (visible.Count <= pageSize) return (visible, null);
		visible.RemoveAt(pageSize);
		var last = visible[^1];
		return (visible, new(last.CreatedAt, last.OperationId));
	}

	private async Task<AcademicNotificationRecentDeliveryDto?> ReadRecentItemAsync(RecentCandidate item, Guid tenantId,
		UserScopeContext callerScope, CancellationToken cancellationToken)
	{
		try
		{
			var definition = await access.RequireSourceViewAsync(item.EventKey, item.SourceType, item.SourceId, cancellationToken);
			var owned = await ReadIntentsAsync(tenantId, item.SourceType, item.SourceId, item.OperationId, item.EventKey,
				intentId: null, includeRuntime: !callerScope.HasTenantWideScope(), cancellationToken);
			var allowed = await AuthorizeIntentsAsync(owned, definition, tenantId, callerScope, cancellationToken);
			return allowed.Count == 0 ? null : new(item.OperationId, item.EventKey, item.SourceType, item.SourceId,
				AcademicNotificationHistoryRows.Utc(item.CreationTime), allowed.Count == owned.Count);
		}
		catch (AbpAuthorizationException) { return null; }
		catch (EntityNotFoundException) { return null; }
		catch (BusinessException error) when (error.Code is NotificationErrorCodes.EventUnknown or
			NotificationErrorCodes.SourceUnavailable or UserScopeErrorCodes.AccessDenied) { return null; }
	}
}
