using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record NotificationDispatchReadinessResult(NotificationDispatchPayload? Payload, DateTimeOffset? DueAt,
	DateTimeOffset? ExpiresAt, string? ExclusionCode, Guid? WhatsAppTemplateBindingId = null);

[RemoteService(false), DisableAuditing]
public sealed class NotificationDispatchReadiness(NotificationDispatchPayloadProtector protector, NotificationIntentSourceReader sources,
	IEnumerable<INotificationRuntimeRecipientResolver> resolvers, NotificationDispatchPolicy policy, NotificationEmailRoute emailRoute,
	NotificationSourceLink links, WhatsAppProviderRoute whatsAppRoute, WhatsAppApprovedTemplates approvals) : ITransientDependency
{
	internal async Task<NotificationDispatchReadinessResult> CheckAsync(NotificationIntent intent, NotificationDispatch dispatch,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		try
		{
			return await CheckCurrentAsync(intent, dispatch, now, cancellationToken);
		}
		catch (BusinessException error) when (error.Code is "DispatchPayloadInvalid" or NotificationErrorCodes.RuntimeSnapshotInvalid or
			NotificationErrorCodes.RuleConfigurationInvalid or NotificationErrorCodes.SourceUnavailable or NotificationErrorCodes.EventUnknown or
			NotificationErrorCodes.TemplateInvalid or NotificationErrorCodes.StateConflict)
		{
			return Exclude(error.Code);
		}
	}

	private async Task<NotificationDispatchReadinessResult> CheckCurrentAsync(NotificationIntent intent, NotificationDispatch dispatch,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		if (dispatch.ExpiresAt <= now) return Exclude("SourceExpired");
		if (!Enum.IsDefined(intent.Origin) || !NotificationRuntimeJson.Deserialize<Guid[]>(intent.TemplateRevisionIdsJson,
			NotificationErrorCodes.TemplateInvalid).Contains(dispatch.TemplateRevisionId)) return Exclude(NotificationErrorCodes.TemplateInvalid);
		var payload = protector.Unprotect(dispatch);
		var source = await sources.LoadAsync(intent, now, cancellationToken);
		if (source.ExclusionCode != null) return Exclude(source.ExclusionCode);
		if (source.Snapshot == null || !source.CurrentRecipients.Contains(payload.SourceRecipient))
			return Exclude(NotificationErrorCodes.RecipientIneligible);
		if (!string.Equals(payload.LinkUrl, links.Resolve(source.Snapshot, payload.SourceRecipient), StringComparison.Ordinal))
			return Exclude("SourceRevisionChanged");

		var matchingResolvers = resolvers.Where(x => x.Supports(intent.EventKey)).Take(2).ToArray();
		if (matchingResolvers.Length != 1) return Exclude("RecipientResolverUnavailable");
		var recipientSource = source with { CapturedRecipients = [payload.SourceRecipient], CurrentRecipients = [payload.SourceRecipient] };
		var current = await matchingResolvers[0].ResolveAsync(recipientSource, cancellationToken);
		var exact = current.Where(x => x.SourceRecipient == payload.SourceRecipient && x.Recipient == payload.Recipient &&
			x.Channel == dispatch.Channel).Take(2).ToArray();
		if (exact.Length != 1) return Exclude("RecipientAssociationChanged");
		var candidate = exact[0];
		if (!candidate.IsEligible) return Exclude(candidate.ExclusionCode ?? NotificationErrorCodes.RecipientIneligible);
		if (!string.Equals(candidate.AssociationRevision, payload.AssociationRevision, StringComparison.Ordinal) ||
			!string.Equals(candidate.Destination, payload.Destination, StringComparison.Ordinal)) return Exclude("RecipientAssociationChanged");

		NotificationEmailProvider? email = null;
		if (dispatch.Channel == CommunicationChannelType.Email)
		{
			email = await emailRoute.FindAsync(cancellationToken);
			if (email == null) return Exclude(NotificationErrorCodes.ProviderUnavailable);
			if (dispatch.ProviderSettingId != email.Id) return Exclude("ProviderChanged");
		}
		var evaluated = await policy.EvaluateAsync(intent, recipientSource, [candidate], now, email?.ConsentRequired == true, cancellationToken);
		if (evaluated.ExclusionCode != null) return Exclude(evaluated.ExclusionCode);
		var decision = evaluated.Recipients.Single();
		if (decision.ExclusionCode != null || !decision.DueAt.HasValue)
			return Exclude(decision.ExclusionCode ?? NotificationErrorCodes.RecipientIneligible);
		var expiresAt = !source.ExpiresAt.HasValue || dispatch.ExpiresAt < source.ExpiresAt ? dispatch.ExpiresAt : source.ExpiresAt;
		if (expiresAt <= decision.DueAt) return Exclude("SourceExpired");
		Guid? bindingId = null;
		if (dispatch.Channel == CommunicationChannelType.WhatsApp)
		{
			var provider = await whatsAppRoute.FindAsync(cancellationToken);
			if (provider == null) return Exclude(NotificationErrorCodes.ProviderUnavailable);
			if (provider.Id != dispatch.ProviderSettingId) return Exclude("ProviderChanged");
			var approved = await approvals.LoadAsync(provider, [dispatch.TemplateRevisionId], cancellationToken);
			if (!approved.TryGetValue(dispatch.TemplateRevisionId, out var binding) ||
				!WhatsAppApprovedTemplates.MatchesLanguage(binding, payload.LanguageCode)) return Exclude("WhatsAppTemplateNotApproved");
			bindingId = binding.Id;
		}
		return new(payload, decision.DueAt, expiresAt, null, bindingId);
	}

	private static NotificationDispatchReadinessResult Exclude(string code) => new(null, null, null, code);
}
