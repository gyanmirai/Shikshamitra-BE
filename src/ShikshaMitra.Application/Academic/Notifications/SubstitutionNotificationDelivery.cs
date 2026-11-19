using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class SubstitutionNotificationDelivery(INotificationIntentWriter writer, NotificationEffectiveRuleResolver rules,
	SubstitutionNotificationSource source, IRepository<CommunicationCampaign, Guid> operations,
	IRepository<NotificationIntent, Guid> intents, IRepository<NotificationDispatch, Guid> dispatches,
	IRepository<CommunicationRecipientSnapshot, Guid> recipients, IAsyncQueryableExecuter queries, ICurrentTenant tenant,
	ICancellationTokenProvider cancellation, IClock clock) : ITransientDependency
{
	internal async Task<NotificationIntentWriteResult> CaptureAsync(SubstitutionRequest duty, Guid? actorId, bool reminder = false)
	{
		var token = cancellation.Token;
		if (tenant.Id is not Guid tenantId || duty.TenantId != tenantId || actorId is not Guid actor || actor == Guid.Empty)
			return new(null, false);
		var current = await source.FindAsync(tenantId, duty.Id, token);
		if (current == null) return new(null, false);
		var eventKey = reminder ? SubstitutionNotificationSource.AcknowledgementDue : SubstitutionNotificationSource.Assigned;
		var scope = current.Scope;
		var rule = await rules.ResolveAsync(eventKey, NotificationRuleScope.Create(scope.AcademicYearId, scope.BoardId, scope.MediumId,
			scope.ClassId, scope.DivisionId), token);
		if (rule is not { IsEnabled: true }) return new(null, false);
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var expiry = SubstitutionNotificationSource.PeriodEnd(duty);
		var occurrence = reminder ? new NotificationDueOccurrenceDto(scope, expiry, rule.Id, rule.Revision) : null;
		return await writer.WriteWithOutcomeAsync(new(eventKey, SubstitutionNotificationSource.Type, duty.Id, SubstitutionNotificationSource.Revision(duty),
			now, [], actor, DueAt: reminder ? now : null, ExpiresAt: expiry, DueOccurrence: occurrence), token);
	}

	internal async Task ApplyStatusAsync(IReadOnlyList<SubstitutionRequest> duties, IReadOnlyList<SubstitutionDto> items, Guid? operationId = null)
	{
		foreach (var item in items)
		{
			item.NotificationStatus = "Not Sent";
			if (item.AcknowledgementStatus == "Sent") item.AcknowledgementStatus = "Not Sent";
		}
		if (tenant.Id is not Guid tenantId || duties.Count == 0) return;
		var scopeDuties = duties.Where(x => x.TenantId == tenantId).ToArray();
		var ids = scopeDuties.Select(x => x.Id).ToArray();
		if (ids.Length == 0) return;

		var evidence = (await recipients.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted && x.RecipientType == "SubstituteTeacher");
		var attempts = (await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);
		var rows = await queries.ToListAsync(
			from intent in await intents.GetQueryableAsync()
			join operation in await operations.GetQueryableAsync() on intent.CampaignId equals (Guid?)operation.Id
			where intent.TenantId == tenantId && operation.TenantId == tenantId && !intent.IsDeleted && !operation.IsDeleted &&
				operation.CampaignType == NotificationCampaignTypes.Academic && operation.SourceModule == CommunicationSourceModule.Academic &&
				intent.SourceId == operation.SourceEntityId && intent.SourceType == SubstitutionNotificationSource.Type &&
				operation.SourceEntityType == SubstitutionNotificationSource.Type && Enumerable.Contains(ids, intent.SourceId) &&
				(intent.EventKey == SubstitutionNotificationSource.Assigned || intent.EventKey == SubstitutionNotificationSource.AcknowledgementDue)
			select new
			{
				intent.SourceId, intent.EventKey, intent.SourceRevision, intent.OccurredAt, operation.Status, OperationId = operation.Id,
				Unknown = attempts.Any(d => d.IntentId == intent.Id && d.CampaignId == operation.Id &&
					d.Status == NotificationDispatchState.UnknownAcceptance),
				SubstituteAccepted = attempts.Any(d => d.IntentId == intent.Id && d.CampaignId == operation.Id &&
					(d.Status == NotificationDispatchState.ProviderAccepted || d.Status == NotificationDispatchState.Delivered ||
						d.Status == NotificationDispatchState.Read) &&
					evidence.Any(e => e.CampaignId == operation.Id && e.Id == d.RecipientSnapshotId))
			}, cancellation.Token);
		var byDuty = rows.ToLookup(x => x.SourceId);
		var byId = items.ToDictionary(x => x.Id);
		foreach (var duty in scopeDuties)
		{
			var revision = SubstitutionNotificationSource.Revision(duty);
			var current = byDuty[duty.Id].Where(x => x.SourceRevision == revision).ToArray();
			var latest = current.Where(x => (!operationId.HasValue || x.OperationId == operationId) &&
				(x.EventKey != SubstitutionNotificationSource.AcknowledgementDue || SubstitutionNotificationSource.CanRemind(duty)))
				.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.EventKey).FirstOrDefault();
			var item = byId[duty.Id];
			if (latest != null)
				item.NotificationStatus = latest.Status switch
				{
					CommunicationCampaignStatus.Scheduled or CommunicationCampaignStatus.Queued or CommunicationCampaignStatus.Sending
						when SubstitutionNotificationSource.IsActive(duty) && SubstitutionNotificationSource.PeriodEnd(duty) > new DateTimeOffset(clock.Now.ToUniversalTime()) => "Queued",
					CommunicationCampaignStatus.Sent or CommunicationCampaignStatus.PartiallyFailed => "Sent",
					_ when latest.Unknown => "Unknown",
					CommunicationCampaignStatus.Failed => "Failed",
					_ => "Not Sent"
				};
			if (item.AcknowledgementStatus == "Not Sent" && current.Any(x => x.SubstituteAccepted))
				item.AcknowledgementStatus = "Sent";
		}
	}
}
