using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public sealed partial class AcademicNotificationOperationAccess(AcademicNotificationAccessService access,
	AcademicNotificationRuleScopeAccess scopes, NotificationIntentSnapshotCodec snapshots, AcademicNotificationReviewService reviews,
	IRepository<CommunicationCampaign, Guid> campaigns, IRepository<NotificationIntent, Guid> intents,
	IRepository<NotificationSendReview, Guid> receipts, IAsyncQueryableExecuter queries,
	NotificationTemplateRevisionService templates, AcademicNotificationCatalogue catalogue) : ITransientDependency
{
	internal sealed record Operation(Guid Id, string? SourceType, Guid? SourceId, CommunicationCampaignStatus Status,
		bool ApprovalRequired, DateTime AuditCreatedAt, DateTimeOffset? ConfirmedAt, DateTimeOffset? PublishedAt,
		DateTimeOffset? ScheduledAt, DateTimeOffset? ApprovedAt, bool ApprovalRejected)
	{
		public DateTimeOffset CreatedAt => (ConfirmedAt ?? (ApprovalRequired ? null : PublishedAt)
			?? AcademicNotificationHistoryRows.Utc(AuditCreatedAt)).ToUniversalTime();
	}
	internal sealed record HistoricalIntent(Guid Id, Guid OperationId, string EventKey, string RuntimeJson,
		NotificationIntentState State, string? ReasonCode, Guid? ActorUserId, DateTimeOffset? ExpiresAt);

	internal sealed record AuthorizedOperation(Operation Record, NotificationEventDefinition Definition, IReadOnlyList<HistoricalIntent> Intents);

	internal async Task<AuthorizedOperation> RequireAsync(Guid operationId, Guid? targetIntentId, Guid tenantId,
		UserScopeContext callerScope, CancellationToken cancellationToken, Guid? templateTestViewerId = null)
	{
		var confirmationTimes = (await receipts.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.OperationId == operationId);
		var operation = await queries.SingleOrDefaultAsync((await OwnedOperationsAsync(tenantId)).Where(x => x.Id == operationId)
			.Select(x => new Operation(x.Id, x.SourceEntityType, x.SourceEntityId, x.Status, x.ApprovalRequired, x.CreationTime,
				confirmationTimes.Select(review => review.ConsumedAt).FirstOrDefault(),
				x.PublishedAt, x.ScheduledAt, x.ApprovedAt, x.Status == CommunicationCampaignStatus.Cancelled &&
					x.RejectionReason == AcademicNotificationManualOperations.ApprovalRejectionReason)), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationCampaign), operationId);
		if (operation.SourceId is not Guid sourceId || operation.SourceType == null) throw new AbpAuthorizationException();

		var ownedIntents = await ReadIntentsAsync(tenantId, operation.SourceType, sourceId, operationId, null,
			intentId: targetIntentId, includeRuntime: !callerScope.HasTenantWideScope(), cancellationToken);
		if (targetIntentId.HasValue && ownedIntents.Count == 0)
			throw new EntityNotFoundException(typeof(NotificationIntent), targetIntentId.Value);
		if (operation.SourceType == NotificationCampaignTypes.AcademicTemplateTestSource)
		{
			if (targetIntentId.HasValue || !templateTestViewerId.HasValue || ownedIntents.Count != 1 ||
				ownedIntents[0].ActorUserId != templateTestViewerId) throw new AbpAuthorizationException();
			var template = await templates.GetAsync(sourceId, cancellationToken);
			if (template.EventKey != ownedIntents[0].EventKey) throw new AbpAuthorizationException();
			return new(operation, catalogue.Get(template.EventKey), ownedIntents);
		}
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
		return new(operation, definition, ownedIntents);
	}

	internal async Task<AcademicNotificationPendingApprovalsDto> ReadPendingApprovalsAsync(Guid tenantId, Guid approverId,
		UserScopeContext callerScope, Guid? afterId, CancellationToken cancellationToken)
	{
		const int pageSize = 10;
		const int scanBatch = 25;
		var owned = await OwnedOperationsAsync(tenantId);
		var assigned = await reviews.AssignedAsync(tenantId, approverId);
		var candidates = from campaign in owned
			join review in assigned on (Guid?)campaign.Id equals review.OperationId
			where campaign.ApprovalRequired && campaign.Status == CommunicationCampaignStatus.PendingApproval
			select new { campaign.Id, campaign.EligibleRecipientCount };
		var visible = new List<AcademicNotificationPendingApprovalDto>(pageSize);
		var cursor = afterId;
		while (true)
		{
			var scan = cursor is Guid after ? candidates.Where(x => x.Id.CompareTo(after) > 0) : candidates;
			var batch = await queries.ToListAsync(scan.OrderBy(x => x.Id).Take(scanBatch), cancellationToken);
			foreach (var candidate in batch)
			{
				AuthorizedOperation operation;
				try { operation = await RequireAsync(candidate.Id, null, tenantId, callerScope, cancellationToken); }
				catch (AbpAuthorizationException) { continue; }
				catch (EntityNotFoundException) { continue; }
				catch (BusinessException error) when (error.Code is NotificationErrorCodes.ReviewChanged or
					NotificationErrorCodes.EventUnknown or NotificationErrorCodes.SourceUnavailable) { continue; }
				if (operation.Record.Status != CommunicationCampaignStatus.PendingApproval) continue;
				if (visible.Count == pageSize) return new(visible, visible[^1].OperationId);
				visible.Add(new(candidate.Id, operation.Definition.EventKey,
					operation.Record.CreatedAt, candidate.EligibleRecipientCount));
			}
			if (batch.Count < scanBatch) return new(visible, null);
			cursor = batch[^1].Id;
		}
	}

	private async Task<AcademicNotificationProtectedReview> ReadProtectedReviewAsync(Guid tenantId, Operation operation,
		CancellationToken cancellationToken)
	{
		var row = await queries.SingleOrDefaultAsync((await receipts.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.OperationId == operation.Id), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationCampaign), operation.Id);
		var review = reviews.ReadReview(row);
		if (review.Request.SourceId != operation.SourceId || review.Request.SourceType != operation.SourceType)
			throw new AbpAuthorizationException();
		return review;
	}

	internal async Task<IReadOnlyList<HistoricalIntent>> ReadIntentsAsync(Guid tenantId, string sourceType, Guid sourceId,
		Guid? operationId, string? eventKey, Guid? intentId, bool includeRuntime, CancellationToken cancellationToken)
	{
		var operations = await OwnedOperationsAsync(tenantId);
		var allIntents = (await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);
		var query = from intent in allIntents
			join campaign in operations on intent.CampaignId equals (Guid?)campaign.Id
			where intent.SourceId == sourceId && intent.SourceType == sourceType &&
				campaign.SourceEntityId == sourceId && campaign.SourceEntityType == sourceType &&
				(!operationId.HasValue || campaign.Id == operationId) && (eventKey == null || intent.EventKey == eventKey) &&
				(!intentId.HasValue || intent.Id == intentId) &&
				!allIntents.Any(other => other.CampaignId == campaign.Id &&
					(other.SourceId != sourceId || other.SourceType != sourceType || other.EventKey != intent.EventKey))
			select new HistoricalIntent(intent.Id, campaign.Id, intent.EventKey,
				includeRuntime ? intent.RuleConfigurationJson : string.Empty, intent.Status, intent.ErrorCode, intent.ActorUserId,
				intent.ExpiresAt);
		return await queries.ToListAsync(query, cancellationToken);
	}

	private async Task<IQueryable<CommunicationCampaign>> OwnedOperationsAsync(Guid tenantId) =>
		(await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.CampaignType == NotificationCampaignTypes.Academic && x.SourceModule == CommunicationSourceModule.Academic);

	internal async Task<HashSet<Guid>> AuthorizeIntentsAsync(IReadOnlyList<HistoricalIntent> candidates, NotificationEventDefinition definition,
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
