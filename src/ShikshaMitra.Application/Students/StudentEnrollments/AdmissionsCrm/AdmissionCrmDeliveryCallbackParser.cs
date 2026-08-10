using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal sealed record AdmissionCrmResolvedDeliveryCallback(
	string Status, string? StatusDetails, string? ProviderMessageId, string CallbackSecret);

internal static class AdmissionCrmDeliveryCallbackParser
{
	internal static AdmissionCrmResolvedDeliveryCallback Resolve(
		string? settingsJson, ApplyAdmissionCrmDeliveryCallbackDto input)
	{
		var mappings = GetMappings(settingsJson);
		var status = Normalize(input.Status) ?? Extract(input.Payload, mappings.StatusPath);
		if (string.IsNullOrWhiteSpace(status)) throw new BusinessException("AdmissionCrm:DeliveryCallbackStatusRequired");
		var secret = Normalize(input.CallbackSecret) ?? Extract(input.Payload, mappings.CallbackSecretPath);
		if (string.IsNullOrWhiteSpace(secret)) throw new BusinessException("AdmissionCrm:DeliveryCallbackSecretRequired");
		return new(status, Normalize(input.StatusDetails) ?? Extract(input.Payload, mappings.StatusDetailsPath),
			Normalize(input.ProviderMessageId) ?? Extract(input.Payload, mappings.ProviderMessageIdPath), secret);
	}

	internal static void EnsureSecret(
		string? settingsJson,
		AdmissionCrmResolvedDeliveryCallback callback,
		AdmissionCrmMessageChannel channel,
		Guid? deliveryLogId = null)
	{
		var configured = GetSecret(settingsJson);
		if (string.IsNullOrWhiteSpace(configured))
			throw new BusinessException("AdmissionCrm:DeliveryCallbackSecretNotConfigured").WithData("Channel", channel);
		if (SecretsEqual(configured, callback.CallbackSecret)) return;
		var exception = new BusinessException("AdmissionCrm:DeliveryCallbackSecretInvalid").WithData("Channel", channel);
		if (deliveryLogId.HasValue) exception.WithData("DeliveryLogId", deliveryLogId.Value);
		throw exception;
	}

	internal static string NormalizeStatus(string status) => status.Trim().ToLowerInvariant() switch
	{
		"queued" => AdmissionCrmMessageDeliveryLog.QueuedStatus,
		"sent" => AdmissionCrmMessageDeliveryLog.SentStatus,
		"delivered" => AdmissionCrmMessageDeliveryLog.DeliveredStatus,
		"failed" => AdmissionCrmMessageDeliveryLog.FailedStatus,
		_ => throw new BusinessException("AdmissionCrm:DeliveryCallbackStatusInvalid").WithData("Status", status)
	};

	internal static string BuildDetails(string status, string? details, string? providerMessageId)
	{
		var result = string.IsNullOrWhiteSpace(details) ? $"Provider callback marked delivery as {status}." : details.Trim();
		if (!string.IsNullOrWhiteSpace(providerMessageId)) result = $"{result} Provider message id: {providerMessageId.Trim()}.";
		return result.Length <= AdmissionCrmConsts.MaxNotesLength ? result : result[..AdmissionCrmConsts.MaxNotesLength];
	}

	internal static string? NormalizeProviderMessageId(string? value) => string.IsNullOrWhiteSpace(value)
		? null
		: Check.Length(value.Trim(), nameof(value), AdmissionCrmConsts.MaxProviderMessageIdLength);

	private static AdmissionCrmCallbackMappings GetMappings(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson)) return new();
		using var parsed = JsonDocument.Parse(settingsJson);
		if (!parsed.RootElement.TryGetProperty("callbackMappings", out var mappings) ||
			mappings.ValueKind != JsonValueKind.Object) return new();
		return new(GetString(mappings, "statusPath"), GetString(mappings, "statusDetailsPath"),
			GetString(mappings, "providerMessageIdPath"), GetString(mappings, "callbackSecretPath"));
	}

	private static string? Extract(IReadOnlyDictionary<string, JsonElement> payload, string? path)
	{
		if (payload.Count == 0 || string.IsNullOrWhiteSpace(path)) return null;
		var normalized = path.Trim();
		if (normalized.StartsWith("$.", StringComparison.Ordinal)) normalized = normalized[2..];
		var segments = normalized.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length == 0 || !TryGetRoot(payload, segments[0], out var current)) return null;
		foreach (var segment in segments.Skip(1))
		{
			if (!TryApplySegment(current, segment, out current)) return null;
		}
		return ToString(current);
	}

	private static bool TryGetRoot(
		IReadOnlyDictionary<string, JsonElement> payload, string segment, out JsonElement value)
	{
		if (payload.TryGetValue(segment, out value)) return true;
		foreach (var item in payload)
		{
			if (!string.Equals(item.Key, segment, StringComparison.OrdinalIgnoreCase)) continue;
			value = item.Value;
			return true;
		}
		value = default;
		return false;
	}

	private static bool TryApplySegment(JsonElement current, string segment, out JsonElement next)
	{
		var propertyName = segment;
		int? index = null;
		var bracket = segment.IndexOf('[', StringComparison.Ordinal);
		if (bracket >= 0 && segment.EndsWith(']'))
		{
			propertyName = segment[..bracket];
			if (int.TryParse(segment[(bracket + 1)..^1], out var parsed)) index = parsed;
		}
		next = current;
		if (!string.IsNullOrWhiteSpace(propertyName) && !TryGetProperty(current, propertyName, out next)) return false;
		if (!index.HasValue) return true;
		if (next.ValueKind != JsonValueKind.Array || index.Value < 0 || index.Value >= next.GetArrayLength())
		{
			next = default;
			return false;
		}
		next = next.EnumerateArray().ElementAt(index.Value);
		return true;
	}

	private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
	{
		if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value)) return true;
		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (var property in element.EnumerateObject())
			{
				if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
				value = property.Value;
				return true;
			}
		}
		value = default;
		return false;
	}

	private static string? ToString(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.String => element.GetString(), JsonValueKind.Number => element.GetRawText(),
		JsonValueKind.True => "true", JsonValueKind.False => "false", JsonValueKind.Null => null,
		JsonValueKind.Undefined => null, _ => element.GetRawText()
	};

	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	private static string? GetString(JsonElement element, string name) =>
		element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	private static string? GetSecret(string? settingsJson)
	{
		if (string.IsNullOrWhiteSpace(settingsJson)) return null;
		using var parsed = JsonDocument.Parse(settingsJson);
		return parsed.RootElement.TryGetProperty("callbackSecret", out var secret) && secret.ValueKind == JsonValueKind.String
			? secret.GetString() : null;
	}
	private static bool SecretsEqual(string expected, string provided)
	{
		if (string.IsNullOrWhiteSpace(provided)) return false;
		var expectedBytes = Encoding.UTF8.GetBytes(expected.Trim());
		var providedBytes = Encoding.UTF8.GetBytes(provided.Trim());
		return expectedBytes.Length == providedBytes.Length &&
			CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
	}

	private sealed record AdmissionCrmCallbackMappings(
		string? StatusPath = "status",
		string? StatusDetailsPath = "statusDetails",
		string? ProviderMessageIdPath = "providerMessageId",
		string? CallbackSecretPath = "callbackSecret");
}
