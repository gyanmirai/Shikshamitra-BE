using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class NotificationDispatchPlanner(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	NotificationIntentSourceReader sources, IEnumerable<INotificationRuntimeRecipientResolver> resolvers,
	NotificationDispatchPolicy policy, NotificationEmailRoute emailRoute, NotificationDispatchMaterializer materializer,
	IRepository<NotificationIntent, Guid> intents, IRepository<CommunicationCampaign, Guid> campaigns,
	IRepository<NotificationDispatch, Guid> dispatches, IRepository<CommunicationRecipientSnapshot, Guid> evidence,
	IAsyncQueryableExecuter queries, IGuidGenerator guids, NotificationOutcomeLock outcomeLock) : ITransientDependency
{
	public async Task PlanAsync(NotificationQueueClaim claim, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || claim.TenantId != tenantId)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var unit = units.Current;
		if (unit?.Options.IsTransactional != true) throw new BusinessException(NotificationErrorCodes.StateConflict);
		var now = Now();
		var intent = await queries.FirstOrDefaultAsync((await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Id == claim.Id && !x.IsDeleted), cancellationToken);
		if (intent == null || intent.Status != NotificationIntentState.Processing || claim.LeaseId == Guid.Empty ||
			intent.LeaseId != claim.LeaseId || intent.LeaseExpiresAt <= now || !intent.LeaseExpiresAt.HasValue)
			throw new BusinessException(NotificationErrorCodes.LeaseLost);

		if (intent.CampaignId is Guid operationId) await outcomeLock.AcquireAsync(operationId, cancellationToken);
		if (intent.CampaignId is not Guid campaignId ||
			!await queries.AnyAsync((await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.Id == campaignId &&
				!x.IsDeleted && x.CampaignType == NotificationCampaignTypes.Academic && x.SourceEntityId == intent.SourceId &&
				x.SourceEntityType == intent.SourceType && (intent.Origin != NotificationIntentOrigin.ReviewedManual ||
					(x.Status == CommunicationCampaignStatus.Queued || x.Status == CommunicationCampaignStatus.Scheduled ||
						x.Status == CommunicationCampaignStatus.Sending) &&
					(!x.ApprovalRequired || x.ApprovedByUserId.HasValue && x.ApprovedAt.HasValue))), cancellationToken))
		{
			await StopAsync(intent, claim.LeaseId, NotificationIntentState.NeedsReview, "NotificationOperationUnavailable", unit, cancellationToken);
			return;
		}

		var (prepared, stopCode) = await PrepareAsync(intent, now, cancellationToken);
		if (stopCode != null)
		{
			var stopState = stopCode switch {
				"SourceExpired" => NotificationIntentState.Expired,
				"RuleDisabled" or "SourceRevisionChanged" or NotificationErrorCodes.SourceUnavailable => NotificationIntentState.Cancelled,
				_ => NotificationIntentState.NeedsReview
			};
			await StopAsync(intent, claim.LeaseId, stopState, stopCode, unit, cancellationToken);
			return;
		}

		intent.CompletePlanning(claim.LeaseId, Now());
		foreach (var item in prepared)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var row = await evidence.InsertAsync(CreateEvidence(tenantId, campaignId, intent.Id, item), cancellationToken: cancellationToken);
			if (item.Dispatch == null) continue;
			item.Dispatch.BindPlanningEvidence(campaignId, row.Id, item.ProviderSettingId);
			await dispatches.InsertAsync(item.Dispatch, cancellationToken: cancellationToken);
		}
		if (prepared.All(x => x.Dispatch == null))
		{
			var needsReview = prepared.FirstOrDefault(x => RequiresReview(x.ExclusionCode));
			intent.CompleteDelivery(needsReview == null ? NotificationIntentState.Completed : NotificationIntentState.NeedsReview,
				needsReview?.ExclusionCode ?? "NoEligibleRecipients");
		}
		await intents.UpdateAsync(intent, cancellationToken: cancellationToken);
		await unit.SaveChangesAsync(cancellationToken);
	}

	private async Task<(IReadOnlyList<NotificationPreparedDispatch> Rows, string? StopCode)>
		PrepareAsync(NotificationIntent intent, DateTimeOffset now, CancellationToken cancellationToken)
	{
		try
		{
			if (!Enum.IsDefined(intent.Origin)) return ([], NotificationErrorCodes.RuntimeSnapshotInvalid);
			var source = await sources.LoadAsync(intent, now, cancellationToken);
			if (source.ExclusionCode != null) return ([], source.ExclusionCode);
			var matching = resolvers.Where(x => x.Supports(intent.EventKey)).Take(2).ToArray();
			if (matching.Length != 1) return ([], "RecipientResolverUnavailable");
			var recipients = await matching[0].ResolveAsync(source, cancellationToken);
			var provider = recipients.Any(x => x.IsEligible && x.Channel == CommunicationChannelType.Email)
				? await emailRoute.FindAsync(cancellationToken) : null;
			var evaluated = await policy.EvaluateAsync(intent, source, recipients, now, provider?.ConsentRequired == true, cancellationToken);
			if (evaluated.ExclusionCode != null) return ([], evaluated.ExclusionCode);
			var rows = await materializer.PrepareAsync(intent, source, evaluated, provider, cancellationToken);
			if (intent.Origin == NotificationIntentOrigin.ReviewedManual &&
				!string.Equals(source.Runtime.ReviewedDeliveryDigest, NotificationDispatchMaterializer.ReviewedDeliveryDigest(rows), StringComparison.Ordinal))
				return ([], NotificationErrorCodes.ReviewChanged);
			return (rows, null);
		}
		catch (BusinessException error) when (error.Code is NotificationErrorCodes.EventUnknown or NotificationErrorCodes.TemplateInvalid or
			NotificationErrorCodes.SourceUnavailable or NotificationErrorCodes.RuntimeSnapshotInvalid or NotificationErrorCodes.RuleConfigurationInvalid)
		{
			return ([], error.Code);
		}
	}

	private async Task StopAsync(NotificationIntent intent, Guid lease, NotificationIntentState state, string code,
		IUnitOfWork unit, CancellationToken cancellationToken)
	{
		intent.StopProcessing(lease, Now(), state, code);
		await intents.UpdateAsync(intent, cancellationToken: cancellationToken);
		await unit.SaveChangesAsync(cancellationToken);
	}

	private CommunicationRecipientSnapshot CreateEvidence(Guid tenantId, Guid campaignId, Guid intentId, NotificationPreparedDispatch item)
	{
		var reference = item.Recipient.Recipient;
		return new(guids.Create(), tenantId) {
			CampaignId = campaignId, RecipientId = reference.StudentId ?? reference.EmployeeId ?? reference.UserId,
			UserId = reference.UserId, RecipientType = reference.Kind, DisplayName = reference.Kind,
			PreferredLanguageCode = item.Language, IsEligible = item.Dispatch != null, BlockedReason = item.ExclusionCode,
			ChannelEligibilityJson = NotificationRuntimeJson.Serialize(new {
				SchemaVersion = 1, IntentId = intentId, SourceRecipient = item.Recipient.SourceRecipient, Recipient = reference,
				item.Recipient.Channel, item.ExclusionCode
			}, NotificationErrorCodes.RuntimeSnapshotInvalid)
		};
	}

	private static bool RequiresReview(string? code) => code is NotificationErrorCodes.RequiredVariableMissing or
		NotificationErrorCodes.TemplateInvalid or NotificationErrorCodes.PrimaryGuardianNotConfigured or NotificationErrorCodes.ProviderUnavailable or
		NotificationErrorCodes.SourceUnavailable or "PreferenceConfigurationInvalid" or "DispatchPayloadTooLarge" or
		"DispatchPayloadInvalid" or "RecipientContentAmbiguous" or "WhatsAppTemplateNotApproved";

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
