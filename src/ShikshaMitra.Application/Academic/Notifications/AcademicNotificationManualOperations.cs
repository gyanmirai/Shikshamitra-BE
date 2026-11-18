using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationManualOperations(IRepository<CommunicationCampaign, Guid> campaigns,
	IRepository<NotificationIntent, Guid> intents, IRepository<IdentityUser, Guid> users, IAsyncQueryableExecuter queries,
	IConfiguration configuration, IGuidGenerator guids, AcademicNotificationCatalogue catalogue, NotificationIntentSnapshotCodec snapshots)
	: ITransientDependency
{
	internal async Task<CommunicationCampaign> GetAsync(Guid tenantId, Guid operationId,
		PreviewAcademicNotificationManualDto request, CancellationToken cancellationToken) =>
		await queries.SingleOrDefaultAsync((await campaigns.GetQueryableAsync()).Where(x => x.Id == operationId &&
			x.TenantId == tenantId && !x.IsDeleted && x.CampaignType == NotificationCampaignTypes.Academic &&
			x.SourceEntityId == request.SourceId && x.SourceEntityType == request.SourceType), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationCampaign), operationId);

	internal async Task<CommunicationCampaign> AcceptAsync(NotificationSendReview review, PreviewAcademicNotificationManualDto request,
		AcademicNotificationManualPlan plan, string idempotencyKey, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var tenantId = review.TenantId!.Value;
		var operation = new CommunicationCampaign(guids.Create(), tenantId)
		{
			CampaignType = NotificationCampaignTypes.Academic, SourceModule = CommunicationSourceModule.Academic,
			SourceEntityId = request.SourceId, SourceEntityType = request.SourceType, SourceReference = $"notification-review:{review.Id:N}",
			Title = "Academic notification: " + catalogue.Get(request.EventKey).Category,
			Body = string.Empty, ChannelsJson = JsonSerializer.Serialize(request.Channels.Order()),
			ApprovalRequired = plan.RequiresApproval, Status = CommunicationCampaignStatus.PendingApproval,
			EligibleRecipientCount = plan.Summary.EligibleRecipientCount
		};
		if (plan.RequiresApproval)
			review.AssignApprover(await FindApproverAsync(tenantId, review.ActorUserId, now, cancellationToken));
		review.Consume(operation.Id, idempotencyKey, now);
		await campaigns.InsertAsync(operation, cancellationToken: cancellationToken);
		if (!plan.RequiresApproval) await QueueAsync(operation, review, request, plan, now, cancellationToken);
		return operation;
	}

	internal async Task QueueAsync(CommunicationCampaign operation, NotificationSendReview review,
		PreviewAcademicNotificationManualDto request, AcademicNotificationManualPlan plan, DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var definition = catalogue.Get(request.EventKey);
		var created = 0;
		foreach (var item in plan.Partitions)
		{
			var ready = item.Evaluation.Prepared;
			if (!ready.Any(x => x.ReviewFingerprint != null)) continue;
			var recipients = NotificationRuntimeJson.Serialize(item.Source.CapturedRecipients, NotificationErrorCodes.RuntimeSnapshotInvalid);
			var revisions = NotificationRuntimeJson.Serialize(item.TemplateRevisions, NotificationErrorCodes.TemplateInvalid);
			var digest = NotificationDispatchMaterializer.ReviewedDeliveryDigest(ready);
			var runtime = snapshots.Serialize(item.Source.Runtime with { ReviewedDeliveryDigest = digest }, definition);
			var deduplicationKey = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
				new { operation.Id, item.Source.Runtime.SourceScope, item.Source.CapturedRecipients })));
			var intent = NotificationIntent.CreateReviewedManual(guids.Create(), review.TenantId!.Value, item.RuleId,
				item.RuleRevision ?? 0, request.EventKey, request.SourceType, request.SourceId, item.Source.Snapshot!.SourceRevision,
				deduplicationKey, now, recipients, revisions, item.Source.ExpiresAt, review.ActorUserId, operation.Id, runtime);
			await intents.InsertAsync(intent, cancellationToken: cancellationToken);
			created++;
		}
		if (created == 0) throw new BusinessException(NotificationErrorCodes.ReviewChanged);
		operation.Status = CommunicationCampaignStatus.Queued;
		operation.PublishedAt = now;
	}

	internal Task SaveAsync(CommunicationCampaign operation, CancellationToken cancellationToken) =>
		campaigns.UpdateAsync(operation, autoSave: true, cancellationToken: cancellationToken);

	internal void RequireAssignedApprover(Guid tenantId, Guid actorId)
	{
		if (ConfiguredApprover(tenantId) != actorId) throw new AbpAuthorizationException();
	}

	private Guid? ConfiguredApprover(Guid tenantId) =>
		Guid.TryParse(configuration[$"Notifications:Academic:Tenants:{tenantId:N}:ApproverUserId"], out var id) &&
			id != Guid.Empty ? id : null;

	private async Task<Guid> FindApproverAsync(Guid tenantId, Guid senderId, DateTimeOffset now, CancellationToken cancellationToken)
	{
		if (ConfiguredApprover(tenantId) is not Guid id || id == senderId)
			throw new BusinessException(NotificationErrorCodes.ApprovalSetupRequired);
		var user = await users.FindAsync(id, cancellationToken: cancellationToken);
		if (user?.TenantId != tenantId || user.IsDeleted || !user.IsActive || user.LockoutEnabled && user.LockoutEnd > now)
			throw new BusinessException(NotificationErrorCodes.ApprovalSetupRequired);
		return id;
	}
}
