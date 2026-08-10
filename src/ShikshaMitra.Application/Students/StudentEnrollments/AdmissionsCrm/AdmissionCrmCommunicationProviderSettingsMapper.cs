using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmCommunicationProviderSettingsMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmCommunicationProviderSettingsDto Merge(
		AdmissionCrmCommunicationProviderSettingsDto stored,
		AdmissionCrmCommunicationProviderSettingsDto? input)
	{
		input ??= new AdmissionCrmCommunicationProviderSettingsDto();
		return new AdmissionCrmCommunicationProviderSettingsDto
		{
			Adapter = input.Adapter,
			PayloadMode = input.PayloadMode,
			ResponseProviderMessageIdPath = input.ResponseProviderMessageIdPath,
			Headers = MergeWriteOnly(stored.Headers, input.Headers),
			Parameters = MergeWriteOnly(stored.Parameters, input.Parameters),
			CallbackSecret = string.IsNullOrWhiteSpace(input.CallbackSecret) ? stored.CallbackSecret : input.CallbackSecret.Trim(),
			CallbackMappings = input.CallbackMappings ?? new AdmissionCrmCommunicationCallbackMappingsDto()
		};
	}

	public static AdmissionCrmCommunicationProviderSettingsDto Sanitize(AdmissionCrmCommunicationProviderSettingsDto settings) => new()
	{
		Adapter = settings.Adapter,
		PayloadMode = settings.PayloadMode,
		ResponseProviderMessageIdPath = settings.ResponseProviderMessageIdPath,
		Headers = settings.Headers.ToDictionary(item => item.Key, _ => string.Empty, StringComparer.OrdinalIgnoreCase),
		Parameters = settings.Parameters.ToDictionary(item => item.Key, _ => string.Empty, StringComparer.OrdinalIgnoreCase),
		CallbackSecret = null,
		HasStoredHeaderValues = settings.Headers.Values.Any(value => !string.IsNullOrWhiteSpace(value)),
		HasStoredParameterValues = settings.Parameters.Values.Any(value => !string.IsNullOrWhiteSpace(value)),
		HasCallbackSecret = !string.IsNullOrWhiteSpace(settings.CallbackSecret),
		CallbackMappings = settings.CallbackMappings
	};

	public static string Serialize(AdmissionCrmCommunicationProviderSettingsDto settings)
	{
		settings ??= new AdmissionCrmCommunicationProviderSettingsDto();
		var adapter = (settings.Adapter ?? string.Empty).Trim().ToLowerInvariant();
		if (adapter is not ("manual" or "manual-queue" or "webhook" or "generic-webhook" or "custom-webhook"))
			throw new BusinessException("AdmissionCrm:UnsupportedCommunicationAdapter").WithData("Adapter", settings.Adapter);
		var payloadMode = (settings.PayloadMode ?? string.Empty).Trim().ToLowerInvariant();
		if (payloadMode is not ("default" or "envelope"))
			throw new BusinessException("AdmissionCrm:UnsupportedCommunicationPayloadMode").WithData("PayloadMode", settings.PayloadMode);
		if (settings.Headers.Count > 20 || settings.Parameters.Count > 20 ||
		    settings.Headers.Any(item => item.Key.Length > 120 || item.Value.Length > 1000) ||
		    settings.Parameters.Any(item => item.Key.Length > 120 || item.Value.Length > 1000))
			throw new BusinessException("AdmissionCrm:CommunicationProviderSettingsTooLarge");
		settings.Adapter = adapter;
		settings.PayloadMode = payloadMode;
		return JsonSerializer.Serialize(settings, JsonOptions);
	}

	public static AdmissionCrmCommunicationProviderSettingsDto Deserialize(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson)) return new AdmissionCrmCommunicationProviderSettingsDto();
		try
		{
			return JsonSerializer.Deserialize<AdmissionCrmCommunicationProviderSettingsDto>(settingsJson, JsonOptions)
			       ?? new AdmissionCrmCommunicationProviderSettingsDto();
		}
		catch (JsonException)
		{
			return new AdmissionCrmCommunicationProviderSettingsDto { Adapter = "unsupported-json" };
		}
	}

	public static void ValidateEndpoint(AdmissionCrmCommunicationProviderSettingsDto settings, string? webhookUrl)
	{
		var adapter = (settings?.Adapter ?? string.Empty).Trim().ToLowerInvariant();
		if (adapter is not ("webhook" or "generic-webhook" or "custom-webhook")) return;
		if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri) ||
		    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
			throw new BusinessException("AdmissionCrm:InvalidCommunicationWebhookUrl").WithData("WebhookUrl", webhookUrl);
	}

	private static Dictionary<string, string> MergeWriteOnly(
		IReadOnlyDictionary<string, string> stored,
		IReadOnlyDictionary<string, string>? input)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, value) in input ?? new Dictionary<string, string>())
		{
			var normalizedKey = key.Trim();
			if (normalizedKey.Length == 0) continue;
			result[normalizedKey] = string.IsNullOrWhiteSpace(value) && stored.TryGetValue(normalizedKey, out var storedValue)
				? storedValue : value;
		}
		return result;
	}
}
