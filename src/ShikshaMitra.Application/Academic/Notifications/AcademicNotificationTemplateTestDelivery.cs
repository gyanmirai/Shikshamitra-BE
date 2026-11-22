using System;
using System.Collections.Generic;
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
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicNotificationTestTarget(string? ExclusionCode, NotificationRecipientRefDto? Recipient = null,
	string? AssociationRevision = null, string? Destination = null, DateTimeOffset? DueAt = null, Guid? ProviderSettingId = null,
	Guid? WhatsAppBindingId = null, IReadOnlyDictionary<string, NotificationVariableValueDto>? WhatsAppVariables = null)
{
	public override string ToString() => nameof(AcademicNotificationTestTarget);
}

[RemoteService(false), DisableAuditing]
public sealed class AcademicNotificationTemplateTestDelivery(IConfiguration configuration, IRepository<IdentityUser, Guid> users,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter queries,
	NotificationTemplateVariableValidator validator, NotificationDispatchPolicy policy, NotificationEmailRoute emailRoute,
	WhatsAppProviderRoute whatsAppRoute, WhatsAppApprovedTemplates approvals, WebPushDispatchSubscriptions push) : ITransientDependency
{
	internal async Task<AcademicNotificationTestTarget> ResolveAsync(Guid tenantId, Guid actorId, CommunicationTemplateRevision revision,
		NotificationEventDefinition definition, DateTimeOffset now, DateTimeOffset expiry, CancellationToken cancellationToken)
	{
		var channel = revision.Channel;
		var configured = configuration[$"Notifications:Academic:Tenants:{tenantId:N}:TemplateTest:{channel}:UserId"];
		var userId = actorId;
		if (configured != null && (!Guid.TryParse(configured, out userId) || userId == Guid.Empty) ||
			configured == null && channel == CommunicationChannelType.WhatsApp)
			return new(NotificationErrorCodes.TemplateTestAccountUnavailable);
		var user = await users.FindAsync(userId, cancellationToken: cancellationToken);
		if (user?.TenantId != tenantId || user.IsDeleted || !user.IsActive || user.LockoutEnabled && user.LockoutEnd > now)
			return new(NotificationErrorCodes.TemplateTestAccountUnavailable);

		var email = user.EmailConfirmed ? user.Email : null;
		var phone = user.PhoneNumberConfirmed ? user.PhoneNumber : null;
		if (AcademicNotificationFamilyRecipients.ChannelExclusion(channel, email, phone, account: true) is { } contactIssue)
			return new(contactIssue);
		var destination = AcademicNotificationFamilyRecipients.Destination(channel, email, phone);
		Guid? providerId = null;
		Guid? bindingId = null;
		var needsEmailConsent = false;
		IReadOnlyDictionary<string, NotificationVariableValueDto>? variables = null;
		switch (channel)
		{
			case CommunicationChannelType.Email:
				var provider = await emailRoute.FindAsync(cancellationToken);
				if (provider == null) return new(NotificationErrorCodes.ProviderUnavailable);
				providerId = provider.Id;
				needsEmailConsent = provider.ConsentRequired;
				break;
			case CommunicationChannelType.WhatsApp:
				if (!revision.IsPublished) return new("WhatsAppTemplateNotApproved");
				var route = await whatsAppRoute.FindAsync(cancellationToken);
				if (route == null) return new(NotificationErrorCodes.ProviderUnavailable);
				var bindings = await approvals.LoadAsync(route, [revision.Id], cancellationToken);
				if (!bindings.TryGetValue(revision.Id, out var binding) || !WhatsAppApprovedTemplates.MatchesLanguage(binding, revision.LanguageCode))
					return new("WhatsAppTemplateNotApproved");
				providerId = route.Id;
				bindingId = binding.Id;
				variables = NotificationTemplatePreviewService.CreateExampleVariables(definition, configuration["App:ClientUrl"]);
				break;
			case CommunicationChannelType.WebPush:
				var subscriptions = await push.LoadAsync([userId], cancellationToken);
				if (!subscriptions.IsConfigured) return new(NotificationErrorCodes.ProviderUnavailable);
				var subscription = subscriptions.Subscriptions[userId].Order().FirstOrDefault();
				if (subscription == Guid.Empty) return new("WebPushSubscriptionUnavailable");
				destination = subscription.ToString("N");
				break;
		}

		var account = new NotificationRecipientRefDto("Staff", UserId: userId);
		var timing = NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false };
		var options = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [], [channel], [],
			timing, revision.LanguageCode);
		var source = new NotificationCurrentSource(tenantId, definition, new(1, new(), options), [account], [account], null, expiry, null);
		var checkedPolicy = await policy.EvaluatePreviewAsync(source,
			[new(account, account, channel, null, destination: destination)], now, now, needsEmailConsent, cancellationToken);
		if (checkedPolicy.ExclusionCode is { } exclusion) return new(exclusion);
		var decision = checkedPolicy.Recipients.Single();
		if (decision.ExclusionCode != null || !decision.DueAt.HasValue)
			return new(decision.ExclusionCode ?? NotificationErrorCodes.RecipientIneligible);
		var association = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { userId, bindingId })));
		return new(null, new(revision.RecipientKind, UserId: userId), association, destination, decision.DueAt,
			providerId, bindingId, variables);
	}

	internal async Task<NotificationDispatchReadinessResult> CheckAsync(NotificationIntent intent, NotificationDispatch dispatch,
		NotificationDispatchPayload payload, DateTimeOffset now, CancellationToken cancellationToken)
	{
		if (intent.TenantId is not Guid tenantId || dispatch.TenantId != tenantId || intent.ActorUserId is not Guid actorId ||
			intent.Origin != NotificationIntentOrigin.ReviewedManual || intent.RuleId.HasValue || intent.ExpiresAt is not { } expiry)
			return new(null, null, null, NotificationErrorCodes.RuntimeSnapshotInvalid);
		if (expiry <= now) return new(null, null, null, "SourceExpired");
		var revision = await queries.FirstOrDefaultAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Id == dispatch.TemplateRevisionId && x.TemplateId == intent.SourceId && x.EventKey == intent.EventKey &&
			x.Channel == dispatch.Channel && !x.IsRetired && !x.IsDeleted), cancellationToken);
		if (revision == null) return new(null, null, null, NotificationErrorCodes.TemplateInvalid);
		if (revision.ContentHash != intent.SourceRevision) return new(null, null, null, "SourceRevisionChanged");
		var definition = validator.GetDefinition(revision.EventKey);
		var target = await ResolveAsync(tenantId, actorId, revision, definition, now, expiry, cancellationToken);
		if (target.ExclusionCode != null) return new(null, null, null, target.ExclusionCode);
		if (payload.Recipient != target.Recipient || payload.SourceRecipient != target.Recipient ||
			payload.AssociationRevision != target.AssociationRevision || !string.Equals(payload.Destination, target.Destination, StringComparison.Ordinal))
			return new(null, null, null, "RecipientAssociationChanged");
		if (dispatch.ProviderSettingId != target.ProviderSettingId) return new(null, null, null, "ProviderChanged");
		return new(payload, target.DueAt, expiry, null, target.WhatsAppBindingId);
	}
}
