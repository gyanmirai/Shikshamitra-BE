using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationTemplateTestSendAppService(AcademicNotificationAccessService access,
	NotificationTemplateRevisionService revisions, AcademicNotificationTemplateTestDelivery delivery,
	NotificationTemplatePreviewService previews, NotificationTemplateVariableValidator validator,
	NotificationDispatchPayloadProtector protector, INotificationPersonalWriteLock writeLock, AcademicNotificationStaffAudit audit,
	IRepository<CommunicationCampaign, Guid> campaigns, IRepository<NotificationIntent, Guid> intents,
	IRepository<CommunicationRecipientSnapshot, Guid> evidence, IRepository<NotificationDispatch, Guid> dispatches, IClock clock)
	: ApplicationService, IAcademicNotificationTemplateTestSendAppService
{
	private const int PerChannelLimit = 5;
	private static readonly TimeSpan LimitWindow = TimeSpan.FromMinutes(15);
	private static readonly TimeSpan DeliveryLifetime = TimeSpan.FromMinutes(10);

	[UnitOfWork(true)]
	public virtual async Task<AcademicNotificationTemplateTestSendDto> SendAsync(Guid templateId,
		AcademicNotificationTemplateTestSendInput input, CancellationToken cancellationToken = default)
	{
		var (tenantId, actorId, _) = await access.RequireStaffActionAsync(cancellationToken);
		var (_, revision) = await revisions.LoadCommandAsync(templateId, input.RevisionId, NotificationTemplateAction.Write, cancellationToken);
		if (!string.Equals(input.ConcurrencyStamp, revision.ConcurrencyStamp, StringComparison.Ordinal)) throw new AbpDbConcurrencyException();
		if (revision.IsRetired) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);

		var channel = revision.Channel;
		if (input.WebPushSubscriptionId.HasValue && channel != CommunicationChannelType.WebPush)
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var channels = NotificationRuntimeJson.Serialize(new[] { channel }, NotificationErrorCodes.TemplateInvalid);
		await writeLock.AcquireAsync([$"academic-template-test:{channel}"], cancellationToken);
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var windowStart = now.UtcDateTime - LimitWindow;
		var queued = await AsyncExecuter.CountAsync((await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.CampaignType == NotificationCampaignTypes.Academic &&
			x.SourceEntityType == NotificationCampaignTypes.AcademicTemplateTestSource && x.ChannelsJson == channels &&
			x.CreationTime > windowStart), cancellationToken);
		if (queued >= PerChannelLimit) throw new BusinessException(NotificationErrorCodes.TemplateTestRateLimited);

		var expiry = now + DeliveryLifetime;
		var definition = validator.GetDefinition(revision.EventKey);
		var target = await delivery.ResolveAsync(tenantId, actorId, revision, definition, now, expiry,
			input.WebPushSubscriptionId, cancellationToken);
		if (target.ExclusionCode != null) throw new BusinessException(target.ExclusionCode);
		var rendered = previews.RenderSynthetic(revision, definition);
		if (!rendered.IsSuccess)
			throw new BusinessException(rendered.ErrorCode ?? NotificationErrorCodes.TemplateInvalid);
		var subject = TestSubject(channel, rendered.Subject!);
		var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rendered.ContentHash + subject)));

		var operation = new CommunicationCampaign(GuidGenerator.Create(), tenantId) {
			CampaignType = NotificationCampaignTypes.Academic, SourceModule = CommunicationSourceModule.Academic,
			SourceEntityType = NotificationCampaignTypes.AcademicTemplateTestSource, SourceEntityId = templateId,
			Title = "Academic notification template test", Body = string.Empty, ChannelsJson = channels,
			EligibleRecipientCount = 1, QueuedCount = 1, Status = CommunicationCampaignStatus.Queued, PublishedAt = now,
			CreationTime = now.UtcDateTime
		};
		var recipient = target.Recipient!;
		var reference = NotificationRuntimeJson.Serialize(recipient, NotificationErrorCodes.RuntimeSnapshotInvalid);
		var intent = NotificationIntent.CreateTemplateTest(GuidGenerator.Create(), tenantId, revision.EventKey, templateId, revision.ContentHash,
			NotificationRuntimeJson.Serialize(new[] { recipient }, NotificationErrorCodes.RuntimeSnapshotInvalid),
			NotificationRuntimeJson.Serialize(new[] { revision.Id }, NotificationErrorCodes.TemplateInvalid), now, expiry, actorId, operation.Id);
		var snapshot = new CommunicationRecipientSnapshot(GuidGenerator.Create(), tenantId) {
			CampaignId = operation.Id, UserId = recipient.UserId, RecipientId = recipient.UserId, RecipientType = "TestAccount",
			DisplayName = "Test account", PreferredLanguageCode = revision.LanguageCode, IsEligible = true
		};
		var dispatchId = GuidGenerator.Create();
		var payload = protector.Protect(new(tenantId, dispatchId, intent.Id, revision.Id, channel, contentHash),
			new(recipient, recipient, target.AssociationRevision!, target.Destination, subject, rendered.Body!, rendered.LinkUrl!,
				revision.LanguageCode, target.WhatsAppVariables));
		var dispatch = new NotificationDispatch(dispatchId, tenantId, intent.Id, revision.Id, reference, intent.Id.ToString("N"),
			channel, dispatchId.ToString("N"), payload, contentHash, intent.DeduplicationKey, target.DueAt!.Value, expiry);
		dispatch.BindPlanningEvidence(operation.Id, snapshot.Id, target.ProviderSettingId);
		await campaigns.InsertAsync(operation, cancellationToken: cancellationToken);
		await intents.InsertAsync(intent, cancellationToken: cancellationToken);
		await evidence.InsertAsync(snapshot, cancellationToken: cancellationToken);
		await dispatches.InsertAsync(dispatch, cancellationToken: cancellationToken);
		await audit.WriteAsync(tenantId, actorId, "QueueTemplateTest", operation.Id, dispatch.Id,
			revision.EventKey, "Queued", null, null, cancellationToken);
		return new(operation.Id, operation.Status, channel, true) { ExpiresAt = expiry };
	}

	private static string TestSubject(CommunicationChannelType channel, string subject)
	{
		if (channel is not (CommunicationChannelType.Email or CommunicationChannelType.InApp)) return subject;
		const string prefix = "[Test] ";
		var length = Math.Min(subject.Length, 256 - prefix.Length);
		if (length < subject.Length && char.IsHighSurrogate(subject[length - 1])) length--;
		return prefix + subject[..length];
	}
}
