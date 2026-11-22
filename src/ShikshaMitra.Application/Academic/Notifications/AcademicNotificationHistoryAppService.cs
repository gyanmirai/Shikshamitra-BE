using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationHistoryAppService(AcademicNotificationAccessService access,
	AcademicNotificationOperationAccess operations, AcademicNotificationHistoryRows history,
	AcademicNotificationManualOperations manual, AcademicNotificationReviewService reviews,
	IDataProtectionProvider protection) : ApplicationService, IAcademicNotificationHistoryAppService
{
	public virtual async Task<AcademicNotificationRecentDeliveriesDto> GetRecentAsync(string? eventKey, string? cursor,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, actorId, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		if (eventKey?.Length > 160 || cursor?.Length > 1024) throw new BusinessException(NotificationErrorCodes.HistoryInvalid);
		var key = string.IsNullOrWhiteSpace(eventKey) ? null : eventKey.Trim();
		var protector = protection.CreateProtector("Academic.Notification.HistoryCursor.v1", tenantId.ToString("N"),
			actorId.ToString("N"), key ?? "(all)");
		var before = ReadRecentCursor(protector, cursor);
		var (items, next) = await operations.ReadRecentAsync(tenantId, callerScope, key, before, cancellationToken);
		return new(items, next == null ? null : protector.Protect(JsonSerializer.Serialize(next)));
	}

	private static AcademicNotificationOperationAccess.RecentKey? ReadRecentCursor(IDataProtector protector, string? cursor)
	{
		if (string.IsNullOrEmpty(cursor)) return null;
		try
		{
			var key = JsonSerializer.Deserialize<AcademicNotificationOperationAccess.RecentKey>(protector.Unprotect(cursor));
			if (key is { OperationId: var id } && id != Guid.Empty && key.CreatedAt != default) return key;
		}
		catch (Exception error) when (error is CryptographicException or JsonException) { }
		throw new BusinessException(NotificationErrorCodes.HistoryInvalid);
	}

	public virtual async Task<AcademicNotificationOperationStatusDto> GetOperationAsync(Guid operationId,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, actorId, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		var authorized = await operations.RequireAsync(operationId, null, tenantId, callerScope, cancellationToken,
			templateTestViewerId: actorId);
		var operation = authorized.Record;
		var deliveries = await history.GetCountsAsync(tenantId, authorized.Intents.Select(x => x.Id).ToArray(), cancellationToken);
		var excluded = await history.CountExcludedAsync(tenantId, [operationId], cancellationToken);
		var outcomes = authorized.Intents.GroupBy(x => (x.State, ReasonCode: AcademicNotificationHistoryRows.SafeReason(x.ReasonCode)))
			.Select(x => new AcademicNotificationIntentCountDto(x.Key.State, x.Key.ReasonCode, x.Count()))
			.OrderBy(x => x.Status).ThenBy(x => x.ReasonCode, StringComparer.Ordinal).ToArray();
		var canReviewApproval = operation.Status == CommunicationCampaignStatus.PendingApproval && operation.ApprovalRequired &&
			manual.IsConfiguredApprover(tenantId, actorId) && await access.CanUseStaffActionsAsync() &&
			await reviews.IsAssignedAsync(tenantId, operationId, actorId, cancellationToken);
		return new(operation.Id, authorized.Definition.EventKey, operation.SourceType!, operation.SourceId!.Value,
			operation.Status, operation.ApprovalRequired, operation.CreatedAt,
			operation.PublishedAt, operation.ScheduledAt, operation.ApprovedAt, deliveries, outcomes, excluded)
		{
			CanReviewApproval = canReviewApproval, ApprovalRejected = operation.ApprovalRejected,
			IsSynthetic = operation.SourceType == NotificationCampaignTypes.AcademicTemplateTestSource
		};
	}

	public virtual async Task<AcademicNotificationPendingApprovalsDto> GetPendingApprovalsAsync(Guid? afterId,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, actorId, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		if (!await access.CanUseStaffActionsAsync()) throw new AbpAuthorizationException();
		return manual.IsConfiguredApprover(tenantId, actorId)
			? await operations.ReadPendingApprovalsAsync(tenantId, actorId, callerScope, afterId, cancellationToken)
			: new([], null);
	}

	public virtual async Task<PagedResultDto<AcademicNotificationHistoryItemDto>> GetHistoryAsync(GetAcademicNotificationHistoryDto input,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, actorId, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.OperationId == Guid.Empty ||
			input.State.HasValue && !Enum.IsDefined(input.State.Value))
			throw new BusinessException(NotificationErrorCodes.HistoryInvalid);
		if (input.SourceType == NotificationCampaignTypes.AcademicTemplateTestSource)
		{
			if (input.OperationId is not Guid id) throw new BusinessException(NotificationErrorCodes.HistoryInvalid);
			var test = await operations.RequireAsync(id, null, tenantId, callerScope, cancellationToken, templateTestViewerId: actorId);
			if (test.Record.SourceType != input.SourceType || test.Record.SourceId != input.SourceId ||
				test.Definition.EventKey != input.EventKey) throw new AbpAuthorizationException();
			return await history.GetPageAsync(tenantId, test.Intents.Select(x => x.Id).ToArray(), [id], test.Definition, input, cancellationToken);
		}
		var definition = await access.RequireSourceViewAsync(input.EventKey, input.SourceType, input.SourceId, cancellationToken);
		var owned = await operations.ReadIntentsAsync(tenantId, input.SourceType, input.SourceId, input.OperationId, input.EventKey,
			intentId: null, includeRuntime: !callerScope.HasTenantWideScope(), cancellationToken);
		var authorized = await operations.AuthorizeIntentsAsync(owned, definition, tenantId, callerScope, cancellationToken);
		var completeOperations = owned.GroupBy(x => x.OperationId).Where(x => x.All(i => authorized.Contains(i.Id)))
			.Select(x => x.Key).ToArray();
		var includeRetryActions = await access.CanUseStaffActionsAsync();
		return await history.GetPageAsync(tenantId, authorized.ToArray(), completeOperations, definition, input, cancellationToken,
			includeRetryActions);
	}
}
