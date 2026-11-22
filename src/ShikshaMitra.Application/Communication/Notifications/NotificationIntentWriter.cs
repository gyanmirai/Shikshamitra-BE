using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing, ExposeServices(typeof(INotificationIntentWriter))]
public sealed class NotificationIntentWriter(ICurrentTenant tenant, IUnitOfWorkManager units, INotificationEventCaptureLock writeLock,
	NotificationIntentCapturePlanner planner, IRepository<CommunicationCampaign, Guid> campaigns, IRepository<NotificationIntent, Guid> intents,
	IRepository<NotificationDispatch, Guid> dispatches, NotificationOutcomeLock outcomeLock, NotificationOutcomeProjector outcomes,
	IAsyncQueryableExecuter executer, IGuidGenerator guids, IDataFilter filters) : INotificationIntentWriter, ITransientDependency
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	public async Task<Guid?> WriteAsync(NotificationEventInput input, CancellationToken cancellationToken) =>
		(await WriteWithOutcomeAsync(input, cancellationToken)).OperationId;

	public async Task<NotificationIntentWriteResult> WriteWithOutcomeAsync(NotificationEventInput input, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var unit = units.Current;
		if (unit?.Options.IsTransactional != true) throw new BusinessException(NotificationErrorCodes.StateConflict);
		var definition = planner.Validate(input);
		var moduleName = definition.EventKey.Split('.')[0];
		if (!Enum.GetNames<CommunicationSourceModule>().Contains(moduleName, StringComparer.Ordinal)) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var module = Enum.Parse<CommunicationSourceModule>(moduleName);
		// A cutoff edit in an already captured school week must not create a second digest operation.
		var eventIdentity = input.EventKey == AcademicNotificationEventKeys.SummaryWeekly && input.DueOccurrence is { } weekly
			? WeeklyIdentity(tenantId, input, weekly)
			: input.DueOccurrence is { Phase: not null } phased
			? JsonSerializer.Serialize(new { tenantId, input.EventKey, input.SourceType, input.SourceId, input.SourceRevision,
				Occurrence = phased.OccurrenceAt.ToUniversalTime(), phased.Scope, phased.Phase }, Json)
			: input.DueOccurrence is { } occurrence
			? JsonSerializer.Serialize(new { tenantId, input.EventKey, input.SourceType, input.SourceId, input.SourceRevision,
				Occurrence = occurrence.OccurrenceAt.ToUniversalTime(), occurrence.Scope }, Json)
			: JsonSerializer.Serialize(new { tenantId, input.EventKey, input.SourceType, input.SourceId, input.SourceRevision,
				Occurrence = definition.TriggerType == NotificationTriggerType.Due ? input.DueAt?.ToUniversalTime() : null }, Json);
		var eventHash = Hash(eventIdentity);
		var reference = "notification-event:" + eventHash;
		await unit.SaveChangesAsync(cancellationToken);
		await writeLock.AcquireAsync(eventHash, cancellationToken);
		List<Guid> previous;
		using (filters.Disable<ISoftDelete>())
		{
			previous = await executer.ToListAsync((await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
				x.CampaignType == NotificationCampaignTypes.Academic && x.SourceReference == reference).Select(x => x.Id).Take(2), cancellationToken);
		}
		if (previous.Count > 1) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (previous.Count == 1)
		{
			var operationId = previous[0];
			var added = AcademicNotificationEventKeys.IsLearningTaskDueReminder(input.EventKey) && input.ActorUserId == null &&
				input.DueOccurrence is { Scope.DivisionId: not null } due && due.OccurrenceAt > input.OccurredAt &&
				(!input.ExpiresAt.HasValue || input.ExpiresAt > input.OccurredAt) &&
				await TryCatchUpAsync(tenantId, operationId, input, definition, eventHash, unit, cancellationToken);
			return new(operationId, false) { AddedRecipients = added };
		}
		var plan = await planner.PlanAsync(input, definition, cancellationToken);
		if (plan.Count == 0) return new(null, false);
		var sendable = plan.Where(x => !x.CaptureOutcome.HasValue).ToArray();
		var operation = new CommunicationCampaign(guids.Create(), tenantId) {
			CampaignType = NotificationCampaignTypes.Academic, SourceModule = module, SourceEntityType = input.SourceType, SourceEntityId = input.SourceId,
			SourceReference = reference, Title = "Academic notification: " + definition.Category, Body = string.Empty,
			PublishedAt = input.OccurredAt.ToUniversalTime(), ScheduledAt = sendable.Length == 0 ? null : sendable.Min(x => x.DueAt),
			Status = sendable.Length == 0 ? (plan.Any(x => x.CaptureOutcome == NotificationIntentState.NeedsReview) ? CommunicationCampaignStatus.Failed : CommunicationCampaignStatus.Cancelled)
				: sendable.Any(x => x.DueAt <= input.OccurredAt) ? CommunicationCampaignStatus.Queued : CommunicationCampaignStatus.Scheduled
		};
		await campaigns.InsertAsync(operation, cancellationToken: cancellationToken);
		await InsertIntentsAsync(tenantId, operation.Id, input, eventHash, plan, cancellationToken);
		await unit.SaveChangesAsync(cancellationToken);
		return new(operation.Id, true);
	}

	private async Task<bool> TryCatchUpAsync(Guid tenantId, Guid operationId, NotificationEventInput input,
		NotificationEventDefinition definition, string eventHash, IUnitOfWork unit, CancellationToken cancellationToken)
	{
		await outcomeLock.AcquireAsync(operationId, cancellationToken);
		var operation = await executer.FirstOrDefaultAsync((await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Id == operationId && !x.IsDeleted && x.CampaignType == NotificationCampaignTypes.Academic &&
			x.SourceModule == CommunicationSourceModule.Academic && x.SourceEntityId == input.SourceId &&
			x.SourceEntityType == input.SourceType), cancellationToken);
		if (operation == null || operation.RejectionReason != null) return false;

		var captured = new HashSet<Guid>();
		using (filters.Disable<ISoftDelete>())
		{
			var owned = (await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.CampaignId == operationId &&
				x.EventKey == input.EventKey && x.SourceType == input.SourceType && x.SourceId == input.SourceId &&
				x.SourceRevision == input.SourceRevision && x.Origin == NotificationIntentOrigin.Automatic);
			var history = await executer.ToListAsync(owned.Select(x => new { x.AffectedRecipientsJson, x.ErrorCode }), cancellationToken);
			if (history.Count == 0 || history.Any(x => x.ErrorCode == NotificationErrorCodes.CancelledBeforeDelivery) ||
				await executer.AnyAsync((await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
					x.CampaignId == operationId && x.ErrorCode == NotificationErrorCodes.CancelledBeforeDelivery &&
					owned.Any(i => i.Id == x.IntentId)), cancellationToken)) return false;
			foreach (var prior in history)
			{
				cancellationToken.ThrowIfCancellationRequested();
				NotificationRecipientRefDto[] recipients;
				try
				{
					recipients = NotificationRuntimeJson.Deserialize<NotificationRecipientRefDto[]>(prior.AffectedRecipientsJson,
						NotificationErrorCodes.RuntimeSnapshotInvalid);
				}
				catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuntimeSnapshotInvalid) { return false; }
				if (recipients.Length is 0 or > 100 || recipients.Any(x =>
					!NotificationSourcePartitioner.ValidReference(x, definition) || x.Kind != "Student")) return false;
				foreach (var recipient in recipients) captured.Add(recipient.StudentId!.Value);
			}
		}

		var plan = await planner.PlanAsync(input, definition, cancellationToken, captured);
		if (plan.Count == 0) return false;
		await InsertIntentsAsync(tenantId, operationId, input, eventHash, plan, cancellationToken);
		if (operation.ScheduledAt == null)
		{
			operation.ScheduledAt = plan.Where(x => !x.CaptureOutcome.HasValue).Select(x => (DateTimeOffset?)x.DueAt).Min();
			if (operation.ScheduledAt != null) await campaigns.UpdateAsync(operation, cancellationToken: cancellationToken);
		}
		await unit.SaveChangesAsync(cancellationToken);
		await outcomes.RefreshLockedAsync(tenantId, operationId, cancellationToken, includeNaturallyCancelled: true);
		return true;
	}

	private async Task InsertIntentsAsync(Guid tenantId, Guid operationId, NotificationEventInput input, string eventHash,
		IReadOnlyList<NotificationIntentCaptureItem> plan, CancellationToken cancellationToken)
	{
		foreach (var item in plan)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var recipients = JsonSerializer.Serialize(item.Recipients, Json);
			var key = Hash(eventHash + ":" + item.Scope.Key + ":" + recipients);
			var intent = new NotificationIntent(guids.Create(), tenantId, item.RuleId, item.RuleRevision, input.EventKey, input.SourceType,
				input.SourceId, input.SourceRevision, key, input.OccurredAt, item.DueAt, recipients, JsonSerializer.Serialize(item.TemplateRevisionIds, Json), item.ExpiresAt, input.ActorUserId);
			if (item.SnapshotJson != "{}") intent.BindRuntimeSnapshot(item.SnapshotJson, NotificationIntentOrigin.Automatic);
			intent.BindAutomaticOperation(operationId);
			if (item.CaptureOutcome.HasValue) intent.StopBeforePlanning(item.CaptureOutcome.Value, item.ErrorCode ?? NotificationErrorCodes.RuntimeSnapshotInvalid);
			await intents.InsertAsync(intent, cancellationToken: cancellationToken);
		}
	}
	private static string WeeklyIdentity(Guid tenantId, NotificationEventInput input, NotificationDueOccurrenceDto occurrence)
	{
		var window = AcademicWeeklySummaryWindow.Read(input.SourceRevision);
		if (window == null || window.Period.End != occurrence.OccurrenceAt || window.Period.ExpiresAt != input.ExpiresAt ||
			occurrence.Scope.DivisionId != input.SourceId)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		return JsonSerializer.Serialize(new { tenantId, input.EventKey, input.SourceType, input.SourceId, occurrence.Scope, window.Week }, Json);
	}

	private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
