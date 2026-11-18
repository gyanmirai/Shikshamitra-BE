using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationHistoryAppService(AcademicNotificationAccessService access,
	AcademicNotificationOperationAccess operations, AcademicNotificationHistoryRows history) : ApplicationService, IAcademicNotificationHistoryAppService
{
	public virtual async Task<AcademicNotificationOperationStatusDto> GetOperationAsync(Guid operationId,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, _, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		var authorized = await operations.RequireAsync(operationId, null, tenantId, callerScope, cancellationToken);
		var operation = authorized.Record;
		var deliveries = await history.GetCountsAsync(tenantId, authorized.Intents.Select(x => x.Id).ToArray(), cancellationToken);
		var excluded = await history.CountExcludedAsync(tenantId, [operationId], cancellationToken);
		var outcomes = authorized.Intents.GroupBy(x => (x.State, ReasonCode: AcademicNotificationHistoryRows.SafeReason(x.ReasonCode)))
			.Select(x => new AcademicNotificationIntentCountDto(x.Key.State, x.Key.ReasonCode, x.Count()))
			.OrderBy(x => x.Status).ThenBy(x => x.ReasonCode, StringComparer.Ordinal).ToArray();
		return new(operation.Id, authorized.Definition.EventKey, operation.SourceType!, operation.SourceId!.Value,
			operation.Status, operation.ApprovalRequired, AcademicNotificationHistoryRows.Utc(operation.CreatedAt),
			operation.PublishedAt, operation.ScheduledAt, operation.ApprovedAt, deliveries, outcomes, excluded);
	}

	public virtual async Task<PagedResultDto<AcademicNotificationHistoryItemDto>> GetHistoryAsync(GetAcademicNotificationHistoryDto input,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, _, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.OperationId == Guid.Empty ||
			input.State.HasValue && !Enum.IsDefined(input.State.Value))
			throw new BusinessException(NotificationErrorCodes.HistoryInvalid);
		var definition = await access.RequireSourceViewAsync(input.EventKey, input.SourceType, input.SourceId, cancellationToken);
		var owned = await operations.ReadIntentsAsync(tenantId, input.SourceType, input.SourceId, input.OperationId, input.EventKey,
			intentId: null, includeRuntime: !callerScope.HasTenantWideScope(), cancellationToken);
		var authorized = await operations.AuthorizeIntentsAsync(owned, definition, tenantId, callerScope, cancellationToken);
		var completeOperations = owned.GroupBy(x => x.OperationId).Where(x => x.All(i => authorized.Contains(i.Id)))
			.Select(x => x.Key).ToArray();
		return await history.GetPageAsync(tenantId, authorized.ToArray(), completeOperations, definition, input, cancellationToken);
	}
}
