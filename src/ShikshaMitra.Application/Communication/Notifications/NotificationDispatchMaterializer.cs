using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record NotificationPreparedDispatch(NotificationRuntimeRecipient Recipient, string Language,
	NotificationDispatch? Dispatch, string? ExclusionCode, Guid? ProviderSettingId = null);

[RemoteService(false), DisableAuditing]
public sealed class NotificationDispatchMaterializer(ICurrentTenant tenant, IRepository<CommunicationTemplateRevision, Guid> revisions,
	IAsyncQueryableExecuter queries, NotificationTemplateRenderer renderer, NotificationSourceLink links,
	NotificationDispatchPayloadProtector protector, IGuidGenerator guids, WhatsAppProviderRoute whatsAppRoute,
	WhatsAppApprovedTemplates approvals, WebPushDispatchSubscriptions webPush) : ITransientDependency
{
	private sealed record DeliveryIdentity(string RecipientFamily, Guid? StudentId, Guid? UserId, string? ExternalDestination,
		CommunicationChannelType Channel);

	internal Task<IReadOnlyList<NotificationPreparedDispatch>> PrepareAsync(NotificationIntent intent, NotificationCurrentSource source,
		NotificationDispatchPolicyResult policy, NotificationEmailProvider? email, CancellationToken cancellationToken) =>
		PrepareCoreAsync(intent, source, policy, email, [], cancellationToken);

	internal Task<IReadOnlyList<NotificationPreparedDispatch>> PreparePreviewAsync(NotificationCurrentSource source,
		NotificationDispatchPolicyResult policy, NotificationEmailProvider? email, IReadOnlyList<Guid> pins,
		CancellationToken cancellationToken) => PrepareCoreAsync(null, source, policy, email, pins, cancellationToken);

	private async Task<IReadOnlyList<NotificationPreparedDispatch>> PrepareCoreAsync(NotificationIntent? intent, NotificationCurrentSource source,
		NotificationDispatchPolicyResult policy, NotificationEmailProvider? email, IReadOnlyList<Guid> previewPins,
		CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || source.TenantId != tenantId ||
			(intent != null && intent.TenantId != tenantId))
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (source.Snapshot == null || policy.ExclusionCode != null) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		var results = new List<NotificationPreparedDispatch>();
		var eligible = new List<NotificationRecipientDispatchDecision>();
		var whatsApp = policy.Recipients.Any(x => x.Recipient.Channel == CommunicationChannelType.WhatsApp &&
			x.ExclusionCode == null && x.DueAt.HasValue) ? await whatsAppRoute.FindAsync(cancellationToken) : null;
		foreach (var decision in policy.Recipients)
		{
			var error = decision.ExclusionCode;
			if (error == null && (decision.Recipient.Channel == CommunicationChannelType.Email && email == null ||
				decision.Recipient.Channel == CommunicationChannelType.WhatsApp && whatsApp == null))
				error = NotificationErrorCodes.ProviderUnavailable;
			if (error != null || !decision.DueAt.HasValue)
				results.Add(new(decision.Recipient, decision.LanguageCode, null, error ?? NotificationErrorCodes.RecipientIneligible));
			else eligible.Add(decision);
		}
		if (eligible.Count == 0) return results;

		var pins = intent == null ? previewPins.ToArray() :
			NotificationRuntimeJson.Deserialize<Guid[]>(intent.TemplateRevisionIdsJson, NotificationErrorCodes.TemplateInvalid);
		var pinned = await LoadPinnedAsync(tenantId, source, pins, cancellationToken);
		var approved = whatsApp == null ? null : await approvals.LoadAsync(whatsApp,
			pinned.Where(x => x.Key.Channel == CommunicationChannelType.WhatsApp).SelectMany(x => x).Select(x => x.Id).ToArray(),
			cancellationToken);
		var overrides = (source.Snapshot.RecipientContent ?? []).ToDictionary(x => x.Recipient);
		var devices = await webPush.LoadAsync(eligible.Where(x => x.Recipient.Channel == CommunicationChannelType.WebPush &&
			x.Recipient.Recipient.UserId.HasValue).Select(x => x.Recipient.Recipient.UserId!.Value).Distinct().ToArray(), cancellationToken);
		foreach (var group in eligible.GroupBy(Identity))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var selected = group.OrderByDescending(x => overrides.ContainsKey(x.Recipient.SourceRecipient)).First();
			var conflictingOverrides = group.Select(x => x.Recipient.SourceRecipient).Distinct().Count(overrides.ContainsKey) > 1;
			if (conflictingOverrides)
			{
				results.Add(new(selected.Recipient, selected.LanguageCode, null, "RecipientContentAmbiguous"));
				continue;
			}
			var content = overrides.GetValueOrDefault(selected.Recipient.SourceRecipient);
			if (selected.Recipient.Channel != CommunicationChannelType.WebPush)
			{
				results.Add(PrepareOne(tenantId, intent, source, selected, content, pinned, email?.Id, whatsApp?.Id, approved));
				continue;
			}
			var subscriptions = selected.Recipient.Recipient.UserId is Guid user ? devices.Subscriptions[user] : [];
			if (!subscriptions.Any())
			{
				results.Add(new(selected.Recipient, selected.LanguageCode, null,
					devices.IsConfigured ? "WebPushSubscriptionUnavailable" : NotificationErrorCodes.ProviderUnavailable));
				continue;
			}
			foreach (var device in subscriptions)
				results.Add(PrepareOne(tenantId, intent, source, selected, content, pinned, email?.Id, whatsApp?.Id, approved, device));
		}
		return results;
	}

	private async Task<ILookup<(string RecipientKind, CommunicationChannelType Channel, string LanguageCode), CommunicationTemplateRevision>>
		LoadPinnedAsync(Guid tenantId, NotificationCurrentSource source, Guid[] pins, CancellationToken cancellationToken)
	{
		if (pins.Length is 0 or > 256 || pins.Contains(Guid.Empty) || pins.Distinct().Count() != pins.Length)
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var headerIds = source.Runtime.Configuration.TemplateIds.ToArray();
		var rows = await queries.ToListAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.IsPublished && x.EventKey == source.Definition.EventKey && x.SchemaVersion == source.Definition.SchemaVersion &&
			Enumerable.Contains(pins, x.Id) && Enumerable.Contains(headerIds, x.TemplateId)), cancellationToken);
		return rows.ToLookup(x => (x.RecipientKind, x.Channel, x.LanguageCode));
	}

	private NotificationPreparedDispatch PrepareOne(Guid tenantId, NotificationIntent? intent, NotificationCurrentSource source,
		NotificationRecipientDispatchDecision decision, NotificationSourceRecipientContentDto? content,
		ILookup<(string RecipientKind, CommunicationChannelType Channel, string LanguageCode), CommunicationTemplateRevision> pinned,
		Guid? emailProvider, Guid? whatsAppProvider, IReadOnlyDictionary<Guid, WhatsAppTemplateBinding>? approved, Guid? pushDeviceId = null)
	{
		var recipient = decision.Recipient;
		var fallback = source.Runtime.Configuration.FallbackLanguage;
		var matches = pinned[(recipient.Recipient.Kind, recipient.Channel, decision.LanguageCode)].ToArray();
		if (matches.Length == 0) matches = pinned[(recipient.Recipient.Kind, recipient.Channel, fallback)].ToArray();
		if (matches.Length != 1) return Exclude(NotificationErrorCodes.TemplateInvalid);
		var revision = matches[0];
		if (recipient.Channel == CommunicationChannelType.WhatsApp &&
			(approved == null || !approved.TryGetValue(revision.Id, out var binding) ||
				!WhatsAppApprovedTemplates.MatchesLanguage(binding, revision.LanguageCode)))
			return Exclude("WhatsAppTemplateNotApproved");
		var link = links.Resolve(content == null ? source.Snapshot!.RelativeLink : content.RelativeLink);
		if (link == null) return Exclude(NotificationErrorCodes.SourceUnavailable);
		var variables = new Dictionary<string, NotificationVariableValueDto>(content?.Variables ?? source.Snapshot!.Variables, StringComparer.Ordinal)
		{
			["source_link"] = new(NotificationVariableType.Link, link),
			["recipient_name"] = new(NotificationVariableType.Text, string.IsNullOrWhiteSpace(recipient.RecipientName)
				? FallbackName(recipient.Recipient.Kind) : recipient.RecipientName)
		};
		if (!string.IsNullOrWhiteSpace(recipient.StudentName))
			variables["student_name"] = new(NotificationVariableType.Text, recipient.StudentName);
		var rendered = renderer.RenderPublished(revision, new(revision.Id, source.Definition.EventKey, source.Definition.SchemaVersion,
			recipient.Recipient.Kind, recipient.Channel, revision.LanguageCode, variables));
		if (!rendered.IsSuccess) return Exclude(rendered.ErrorCode ?? NotificationErrorCodes.TemplateInvalid);
		if (string.IsNullOrWhiteSpace(recipient.AssociationRevision)) return Exclude(NotificationErrorCodes.RecipientIneligible);

		var providerSettingId = recipient.Channel switch {
			CommunicationChannelType.Email => emailProvider,
			CommunicationChannelType.WhatsApp => whatsAppProvider,
			_ => null
		};
		if (intent == null) return new(recipient, revision.LanguageCode, null, null, providerSettingId);

		var identity = Identity(decision);
		var context = Hash(new { intent.Id, identity.RecipientFamily, identity.StudentId, identity.UserId, recipient.Recipient.FamilyRelationship });
		var deduplicationKey = pushDeviceId.HasValue ? Hash(new { intent.Id, context, recipient.Channel, pushDeviceId }) :
			Hash(new { intent.Id, context, recipient.Channel });
		var id = guids.Create();
		var key = new NotificationDispatchPayloadKey(tenantId, id, intent.Id, revision.Id, recipient.Channel, rendered.ContentHash!);
		string encrypted;
		try
		{
			encrypted = protector.Protect(key, new(recipient.SourceRecipient, recipient.Recipient, recipient.AssociationRevision,
				pushDeviceId?.ToString("N") ?? recipient.Destination, rendered.Subject, rendered.Body!, rendered.LinkUrl!, revision.LanguageCode,
				recipient.Channel == CommunicationChannelType.WhatsApp ? variables : null));
		}
		catch (BusinessException error) when (error.Code is "DispatchPayloadTooLarge" or "DispatchPayloadInvalid")
		{
			return Exclude(error.Code);
		}
		var fingerprint = Hash(new { intent.Id, recipient.Recipient, recipient.AssociationRevision, recipient.Channel });
		var reference = NotificationRuntimeJson.Serialize(recipient.Recipient, NotificationErrorCodes.RuntimeSnapshotInvalid);
		var expiresAt = !source.ExpiresAt.HasValue || intent.ExpiresAt < source.ExpiresAt ? intent.ExpiresAt : source.ExpiresAt;
		var dispatch = new NotificationDispatch(id, tenantId, intent.Id, revision.Id, reference, context, recipient.Channel,
			fingerprint, encrypted, rendered.ContentHash!, deduplicationKey, decision.DueAt!.Value, expiresAt);
		return new(recipient, revision.LanguageCode, dispatch, null, providerSettingId);

		NotificationPreparedDispatch Exclude(string code) => new(recipient, decision.LanguageCode, null, code);
	}

	private static DeliveryIdentity Identity(NotificationRecipientDispatchDecision decision)
	{
		var candidate = decision.Recipient;
		var recipient = candidate.Recipient;
		var isFamily = recipient.Kind is "Guardian" or "StudentAccount";
		var destination = isFamily && candidate.Channel is CommunicationChannelType.Email or CommunicationChannelType.WhatsApp
			? candidate.Destination : null;
		if (destination != null && candidate.Channel == CommunicationChannelType.Email) destination = destination.ToLowerInvariant();
		return new(isFamily ? recipient.Kind : "Staff", recipient.StudentId, destination == null ? recipient.UserId : null,
			destination, candidate.Channel);
	}

	private static string FallbackName(string kind) => kind switch {
		"Guardian" => "Guardian",
		"StudentAccount" => "Student",
		_ => "Staff member"
	};

	private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
