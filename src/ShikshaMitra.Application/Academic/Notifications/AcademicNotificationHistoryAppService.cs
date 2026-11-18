using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationHistoryAppService(AcademicNotificationAccessService access,
	AcademicNotificationRuleScopeAccess scopes, NotificationIntentSnapshotCodec snapshots,
	AcademicNotificationReviewService reviews, AcademicNotificationHistoryRows history,
	IRepository<CommunicationCampaign, Guid> campaigns, IRepository<NotificationIntent, Guid> intents,
	IRepository<NotificationSendReview, Guid> receipts) : ApplicationService, IAcademicNotificationHistoryAppService
{
	private sealed record Operation(Guid Id, string? SourceType, Guid? SourceId, CommunicationCampaignStatus Status,
		bool ApprovalRequired, DateTime CreatedAt, DateTimeOffset? PublishedAt, DateTimeOffset? ScheduledAt, DateTimeOffset? ApprovedAt);
	private sealed record HistoricalIntent(Guid Id, Guid OperationId, string EventKey, string RuntimeJson,
		NotificationIntentState State, string? ReasonCode);

	public virtual async Task<AcademicNotificationOperationStatusDto> GetOperationAsync(Guid operationId,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, _, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		var operation = await AsyncExecuter.SingleOrDefaultAsync((await OwnedOperationsAsync(tenantId)).Where(x => x.Id == operationId)
			.Select(x => new Operation(x.Id, x.SourceEntityType, x.SourceEntityId, x.Status, x.ApprovalRequired, x.CreationTime,
				x.PublishedAt, x.ScheduledAt, x.ApprovedAt)), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationCampaign), operationId);
		if (operation.SourceId is not Guid sourceId || operation.SourceType == null) throw new AbpAuthorizationException();

		var ownedIntents = await ReadIntentsAsync(tenantId, operation.SourceType, sourceId, operationId, null,
			includeRuntime: !callerScope.HasTenantWideScope(), cancellationToken);
		var eventKeys = ownedIntents.Select(x => x.EventKey).Distinct(StringComparer.Ordinal).Take(2).ToArray();
		if (eventKeys.Length > 1) throw new AbpAuthorizationException();
		var pending = ownedIntents.Count == 0 ? await ReadProtectedReviewAsync(tenantId, operation, cancellationToken) : null;
		var eventKey = pending?.Request.EventKey ?? eventKeys.SingleOrDefault() ?? throw new AbpAuthorizationException();
		var definition = await access.RequireSourceViewAsync(eventKey, operation.SourceType, sourceId, cancellationToken);
		if (pending != null && !callerScope.HasTenantWideScope())
		{
			if (pending.Scopes.Length == 0) throw new AbpAuthorizationException();
			foreach (var scope in pending.Scopes)
				await RequireScopeAsync(scope, tenantId, callerScope, cancellationToken);
		}
		var allowed = await AuthorizeIntentsAsync(ownedIntents, definition, tenantId, callerScope, cancellationToken);
		if (allowed.Count != ownedIntents.Count) throw new AbpAuthorizationException();
		var ids = allowed.ToArray();
		var deliveries = await history.GetCountsAsync(tenantId, ids, cancellationToken);
		var excluded = await history.CountExcludedAsync(tenantId, [operationId], cancellationToken);
		var outcomes = ownedIntents.GroupBy(x => (x.State, ReasonCode: AcademicNotificationHistoryRows.SafeReason(x.ReasonCode)))
			.Select(x => new AcademicNotificationIntentCountDto(x.Key.State, x.Key.ReasonCode, x.Count()))
			.OrderBy(x => x.Status).ThenBy(x => x.ReasonCode, StringComparer.Ordinal).ToArray();
		return new(operation.Id, eventKey, operation.SourceType, sourceId, operation.Status, operation.ApprovalRequired,
			AcademicNotificationHistoryRows.Utc(operation.CreatedAt), operation.PublishedAt, operation.ScheduledAt, operation.ApprovedAt,
			deliveries, outcomes, excluded);
	}

	public virtual async Task<PagedResultDto<AcademicNotificationHistoryItemDto>> GetHistoryAsync(GetAcademicNotificationHistoryDto input,
		CancellationToken cancellationToken = default)
	{
		var (tenantId, _, callerScope) = await access.RequireHistoryViewerAsync(cancellationToken);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.OperationId == Guid.Empty ||
			input.State.HasValue && !Enum.IsDefined(input.State.Value))
			throw new BusinessException(NotificationErrorCodes.HistoryInvalid);
		var definition = await access.RequireSourceViewAsync(input.EventKey, input.SourceType, input.SourceId, cancellationToken);
		var owned = await ReadIntentsAsync(tenantId, input.SourceType, input.SourceId, input.OperationId, input.EventKey,
			includeRuntime: !callerScope.HasTenantWideScope(), cancellationToken);
		var authorized = await AuthorizeIntentsAsync(owned, definition, tenantId, callerScope, cancellationToken);
		var completeOperations = owned.GroupBy(x => x.OperationId).Where(x => x.All(i => authorized.Contains(i.Id)))
			.Select(x => x.Key).ToArray();
		return await history.GetPageAsync(tenantId, authorized.ToArray(), completeOperations, definition, input, cancellationToken);
	}

	private async Task<AcademicNotificationProtectedReview> ReadProtectedReviewAsync(Guid tenantId, Operation operation,
		CancellationToken cancellationToken)
	{
		var row = await AsyncExecuter.SingleOrDefaultAsync((await receipts.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.OperationId == operation.Id), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationCampaign), operation.Id);
		var review = reviews.ReadReview(row);
		if (review.Request.SourceId != operation.SourceId || review.Request.SourceType != operation.SourceType)
			throw new AbpAuthorizationException();
		return review;
	}

	private async Task<IReadOnlyList<HistoricalIntent>> ReadIntentsAsync(Guid tenantId, string sourceType, Guid sourceId,
		Guid? operationId, string? eventKey, bool includeRuntime, CancellationToken cancellationToken)
	{
		var operations = await OwnedOperationsAsync(tenantId);
		var allIntents = (await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);
		var query = from intent in allIntents
			join campaign in operations on intent.CampaignId equals (Guid?)campaign.Id
			where intent.SourceId == sourceId && intent.SourceType == sourceType &&
				campaign.SourceEntityId == sourceId && campaign.SourceEntityType == sourceType &&
				(!operationId.HasValue || campaign.Id == operationId) && (eventKey == null || intent.EventKey == eventKey) &&
				!allIntents.Any(other => other.CampaignId == campaign.Id &&
					(other.SourceId != sourceId || other.SourceType != sourceType || other.EventKey != intent.EventKey))
			select new HistoricalIntent(intent.Id, campaign.Id, intent.EventKey,
				includeRuntime ? intent.RuleConfigurationJson : string.Empty, intent.Status, intent.ErrorCode);
		return await AsyncExecuter.ToListAsync(query, cancellationToken);
	}

	private async Task<IQueryable<CommunicationCampaign>> OwnedOperationsAsync(Guid tenantId) =>
		(await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.CampaignType == NotificationCampaignTypes.Academic && x.SourceModule == CommunicationSourceModule.Academic);

	private async Task<HashSet<Guid>> AuthorizeIntentsAsync(IReadOnlyList<HistoricalIntent> candidates, NotificationEventDefinition definition,
		Guid tenantId, UserScopeContext callerScope, CancellationToken cancellationToken)
	{
		if (callerScope.HasTenantWideScope()) return candidates.Select(x => x.Id).ToHashSet();
		var allowed = new HashSet<Guid>();
		var decisions = new Dictionary<NotificationSourceScopeDto, bool>();
		foreach (var candidate in candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (candidate.RuntimeJson == "{}") continue;
			NotificationSourceScopeDto scope;
			try { scope = snapshots.Deserialize(candidate.RuntimeJson, definition).SourceScope; }
			catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuntimeSnapshotInvalid) { continue; }
			if (!decisions.TryGetValue(scope, out var canAccess))
			{
				try
				{
					await RequireScopeAsync(scope, tenantId, callerScope, cancellationToken);
					canAccess = true;
				}
				catch (AbpAuthorizationException) { canAccess = false; }
				decisions[scope] = canAccess;
			}
			if (canAccess) allowed.Add(candidate.Id);
		}
		return allowed;
	}

	private Task<NotificationRuleScope> RequireScopeAsync(NotificationSourceScopeDto scope, Guid tenantId,
		UserScopeContext callerScope, CancellationToken cancellationToken) =>
		scopes.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId),
			tenantId, callerScope, cancellationToken);
}
