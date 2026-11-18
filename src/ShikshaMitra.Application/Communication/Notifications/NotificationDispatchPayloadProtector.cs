using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record NotificationDispatchPayloadKey(Guid TenantId, Guid DispatchId, Guid IntentId, Guid TemplateRevisionId,
	CommunicationChannelType Channel, string ContentHash);

internal sealed record NotificationDispatchPayload(NotificationRecipientRefDto SourceRecipient, NotificationRecipientRefDto Recipient,
	string AssociationRevision, string? Destination, string? Subject, string Body, string LinkUrl,
	string LanguageCode, IReadOnlyDictionary<string, NotificationVariableValueDto>? WhatsAppVariables)
{
	public override string ToString() => nameof(NotificationDispatchPayload);
}

[RemoteService(false), DisableAuditing]
public sealed class NotificationDispatchPayloadProtector(IDataProtectionProvider protection, ICurrentTenant tenant) : ITransientDependency
{
	private const int MaxProtectedLength = 131_072;
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
		PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		RespectRequiredConstructorParameters = true, MaxDepth = 16, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	internal string Protect(NotificationDispatchPayloadKey key, NotificationDispatchPayload payload)
	{
		RequireTenant(key.TenantId);
		try
		{
			Validate(key, payload);
			var sealedContent = Protector(key).Protect(JsonSerializer.Serialize(payload, Json));
			if (sealedContent.Length > MaxProtectedLength) throw new BusinessException("DispatchPayloadTooLarge");
			return sealedContent;
		}
		catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException or FormatException)
		{
			throw new BusinessException("DispatchPayloadInvalid");
		}
	}

	internal NotificationDispatchPayload Unprotect(NotificationDispatch dispatch)
	{
		var key = new NotificationDispatchPayloadKey(dispatch.TenantId ?? Guid.Empty,
			dispatch.Id, dispatch.IntentId, dispatch.TemplateRevisionId, dispatch.Channel, dispatch.ContentHash);
		RequireTenant(key.TenantId);
		try
		{
			if (dispatch.ProtectedMessageJson is not { Length: > 0 and <= MaxProtectedLength }) throw new CryptographicException();
			var payload = JsonSerializer.Deserialize<NotificationDispatchPayload>(Protector(key).Unprotect(dispatch.ProtectedMessageJson), Json);
			Validate(key, payload);
			return payload!;
		}
		catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException or FormatException)
		{
			throw new BusinessException("DispatchPayloadInvalid");
		}
	}

	private void RequireTenant(Guid tenantId)
	{
		if (tenantId == Guid.Empty || tenant.Id != tenantId) throw new BusinessException(NotificationErrorCodes.TenantRequired);
	}

	private static void Validate(NotificationDispatchPayloadKey key, NotificationDispatchPayload? payload)
	{
		if (key.DispatchId == Guid.Empty || key.IntentId == Guid.Empty || key.TemplateRevisionId == Guid.Empty ||
			string.IsNullOrWhiteSpace(key.ContentHash) || payload?.SourceRecipient == null || payload.Recipient == null ||
			payload.AssociationRevision is not { Length: > 0 and <= 128 } || payload.LanguageCode is not { Length: > 0 and <= 16 } ||
			payload.LinkUrl is not { Length: > 0 and <= 2048 } || string.IsNullOrWhiteSpace(payload.Body))
			throw new CryptographicException();
		if (key.Channel == CommunicationChannelType.WhatsApp && payload.WhatsAppVariables == null ||
			key.Channel != CommunicationChannelType.WhatsApp && payload.WhatsAppVariables != null)
			throw new CryptographicException();
	}

	private IDataProtector Protector(NotificationDispatchPayloadKey key) =>
		protection.CreateProtector("Notifications.Dispatch.Payload.v1", key.TenantId.ToString("N"), key.DispatchId.ToString("N"),
			key.IntentId.ToString("N"), key.TemplateRevisionId.ToString("N"), key.Channel.ToString(), key.ContentHash);
}
