using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
	IAsyncQueryableExecuter executer, IGuidGenerator guids, IDataFilter filters) : INotificationIntentWriter, ITransientDependency
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	public async Task<Guid?> WriteAsync(NotificationEventInput input, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var unit = units.Current;
		if (unit?.Options.IsTransactional != true) throw new BusinessException(NotificationErrorCodes.StateConflict);
		var definition = planner.Validate(input);
		var moduleName = definition.EventKey.Split('.')[0];
		if (!Enum.GetNames<CommunicationSourceModule>().Contains(moduleName, StringComparer.Ordinal)) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var module = Enum.Parse<CommunicationSourceModule>(moduleName);
		var eventHash = Hash(JsonSerializer.Serialize(new { tenantId, input.EventKey, input.SourceType, input.SourceId, input.SourceRevision,
			Occurrence = definition.TriggerType == NotificationTriggerType.Due ? input.DueAt?.ToUniversalTime() : null }, Json));
		var reference = "notification-event:" + eventHash;
		await unit.SaveChangesAsync(cancellationToken);
		await writeLock.AcquireAsync(eventHash, cancellationToken);
		using (filters.Disable<ISoftDelete>())
		{
			var previous = await executer.ToListAsync((await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
				x.CampaignType == NotificationCampaignTypes.Academic && x.SourceReference == reference).Select(x => x.Id).Take(2), cancellationToken);
			if (previous.Count > 1) throw new BusinessException(NotificationErrorCodes.StateConflict);
			if (previous.Count == 1) return previous[0];
		}
		var plan = await planner.PlanAsync(input, definition, cancellationToken);
		if (plan.Count == 0) return null;
		var sendable = plan.Where(x => !x.CaptureOutcome.HasValue).ToArray();
		var operation = new CommunicationCampaign(guids.Create(), tenantId) {
			CampaignType = NotificationCampaignTypes.Academic, SourceModule = module, SourceEntityType = input.SourceType, SourceEntityId = input.SourceId,
			SourceReference = reference, Title = "Academic notification: " + definition.Category, Body = string.Empty,
			PublishedAt = input.OccurredAt.ToUniversalTime(), ScheduledAt = sendable.Length == 0 ? null : sendable.Min(x => x.DueAt),
			Status = sendable.Length == 0 ? (plan.Any(x => x.CaptureOutcome == NotificationIntentState.NeedsReview) ? CommunicationCampaignStatus.Failed : CommunicationCampaignStatus.Cancelled)
				: sendable.Any(x => x.DueAt <= input.OccurredAt) ? CommunicationCampaignStatus.Queued : CommunicationCampaignStatus.Scheduled
		};
		await campaigns.InsertAsync(operation, cancellationToken: cancellationToken);
		foreach (var item in plan)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var recipients = JsonSerializer.Serialize(item.Recipients, Json);
			var key = Hash(eventHash + ":" + item.Scope.Key + ":" + recipients);
			var intent = new NotificationIntent(guids.Create(), tenantId, item.RuleId, item.RuleRevision, input.EventKey, input.SourceType,
				input.SourceId, input.SourceRevision, key, input.OccurredAt, item.DueAt, recipients, JsonSerializer.Serialize(item.TemplateRevisionIds, Json), item.ExpiresAt, input.ActorUserId);
			if (item.SnapshotJson != "{}") intent.BindRuntimeSnapshot(item.SnapshotJson, NotificationIntentOrigin.Automatic);
			intent.BindAutomaticOperation(operation.Id);
			if (item.CaptureOutcome.HasValue) intent.StopBeforePlanning(item.CaptureOutcome.Value, item.ErrorCode ?? NotificationErrorCodes.RuntimeSnapshotInvalid);
			await intents.InsertAsync(intent, cancellationToken: cancellationToken);
		}
		await unit.SaveChangesAsync(cancellationToken); return operation.Id;
	}
	private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
