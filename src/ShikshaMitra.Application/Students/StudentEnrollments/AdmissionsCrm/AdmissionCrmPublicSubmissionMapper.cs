using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPublicSubmissionMapper
{
	internal static Dictionary<string, string?> ValidateAndMap(
		AdmissionInquiryFormTemplate template,
		IReadOnlyList<AdmissionCrmFormFieldDto> fields,
		PublicAdmissionInquirySubmitDto input)
	{
		if (!input.ConsentAccepted)
		{
			throw new BusinessException("AdmissionCrm:ConsentRequired");
		}

		if (!string.Equals(input.ConsentVersion, template.ConcurrencyStamp, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:ConsentVersionExpired");
		}

		if (input.CommunicationPreferences.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:CommunicationPreferenceRequired");
		}

		foreach (var preference in input.CommunicationPreferences)
		{
			Check.Length(preference.Trim(), nameof(input.CommunicationPreferences), 80);
		}

		foreach (var field in fields.Where(field => field.Required && IsFieldVisible(field, input.FieldValues)))
		{
			if (string.IsNullOrWhiteSpace(GetSubmittedOrDefaultFieldValue(field, input.FieldValues)))
			{
				throw new BusinessException("AdmissionCrm:RequiredPublicFieldMissing").WithData("FieldKey", field.Key);
			}
		}

		var mapped = MapSystemFields(fields, input.FieldValues);
		if (string.IsNullOrWhiteSpace(GetValue(mapped, "PrimaryContactNo")) &&
			string.IsNullOrWhiteSpace(GetValue(mapped, "Email")))
		{
			throw new BusinessException("AdmissionCrm:PublicSubmissionContactRequired");
		}

		_ = GetRequiredValue(mapped, "AdmissionIntent", "AdmissionCrm:PublicSubmissionIntentRequired");
		return mapped;
	}

	internal static Dictionary<string, string?> MapSystemFields(
		IReadOnlyList<AdmissionCrmFormFieldDto> fields,
		IReadOnlyDictionary<string, JsonElement> values)
	{
		var mapped = MapTrackingFields(values);
		foreach (var field in fields.Where(field => !string.IsNullOrWhiteSpace(field.SystemField)))
		{
			if (!IsFieldVisible(field, values))
			{
				continue;
			}

			var value = GetSubmittedOrDefaultFieldValue(field, values);
			if (!string.IsNullOrWhiteSpace(value))
			{
				mapped[field.SystemField!] = value;
			}
		}

		return mapped;
	}

	internal static Guid? ResolveTrackedOwnerUserId(
		AdmissionInquiryChannel channel,
		IReadOnlyDictionary<string, string?> mapped,
		AdmissionCrmChannelSettingDto channelSetting,
		AdmissionCrmLeadRoutingSettingsDto routingSettings)
	{
		if (channel != AdmissionInquiryChannel.StaffSharedLink)
		{
			return null;
		}

		var ownerUserId = ParseGuid(GetValue(mapped, "OwnerUserId"));
		if (!ownerUserId.HasValue || ownerUserId.Value == Guid.Empty)
		{
			return null;
		}

		return channelSetting.DefaultOwnerUserId == ownerUserId ||
			(routingSettings.IsEnabled &&
			 (routingSettings.DefaultOwnerUserId == ownerUserId ||
			  routingSettings.OwnerPoolUserIds.Contains(ownerUserId.Value) ||
			  routingSettings.Rules.Any(rule => rule.OwnerUserId == ownerUserId)))
			? ownerUserId
			: null;
	}

	internal static string? GetValue(IReadOnlyDictionary<string, string?> values, string key) =>
		values.TryGetValue(key, out var value) ? value : null;

	internal static string GetRequiredValue(
		IReadOnlyDictionary<string, string?> values,
		string key,
		string errorCode)
	{
		var value = GetValue(values, key);
		return string.IsNullOrWhiteSpace(value) ? throw new BusinessException(errorCode) : value;
	}

	internal static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;

	internal static DateTimeOffset? ParseDateTimeOffset(string? value) =>
		DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
			? parsed
			: null;

	internal static string SerializeCommunicationPreferences(IReadOnlyList<string> preferences) =>
		JsonSerializer.Serialize(
			preferences.Where(value => !string.IsNullOrWhiteSpace(value))
				.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
			AdmissionCrmPublicFormat.JsonOptions);

	private static Dictionary<string, string?> MapTrackingFields(IReadOnlyDictionary<string, JsonElement> values)
	{
		var mapped = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		var campaign = GetFirstTrackingValue(values, "CampaignName", "campaignName", "campaign", "utm_campaign", "utmCampaign");
		if (!string.IsNullOrWhiteSpace(campaign))
		{
			mapped["CampaignName"] = campaign;
		}

		var owner = GetFirstTrackingValue(values, "OwnerUserId", "ownerUserId", "staffUserId", "sharedByUserId", "referrerUserId");
		if (!string.IsNullOrWhiteSpace(owner))
		{
			mapped["OwnerUserId"] = owner;
		}

		return mapped;
	}

	private static bool IsFieldVisible(
		AdmissionCrmFormFieldDto field,
		IReadOnlyDictionary<string, JsonElement> values)
	{
		if (string.IsNullOrWhiteSpace(field.VisibleWhenFieldKey))
		{
			return true;
		}

		if (!values.TryGetValue(field.VisibleWhenFieldKey.Trim(), out var controllingValue))
		{
			return false;
		}

		var actual = ExtractString(controllingValue);
		return string.IsNullOrWhiteSpace(field.VisibleWhenValue)
			? !string.IsNullOrWhiteSpace(actual)
			: string.Equals(actual?.Trim(), field.VisibleWhenValue.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static string? GetSubmittedOrDefaultFieldValue(
		AdmissionCrmFormFieldDto field,
		IReadOnlyDictionary<string, JsonElement> values)
	{
		if (values.TryGetValue(field.Key, out var submittedValue))
		{
			var extracted = ExtractString(submittedValue);
			if (!string.IsNullOrWhiteSpace(extracted))
			{
				return extracted;
			}
		}

		return string.IsNullOrWhiteSpace(field.DefaultValue) ? null : field.DefaultValue.Trim();
	}

	private static string? GetFirstTrackingValue(
		IReadOnlyDictionary<string, JsonElement> values,
		params string[] keys)
	{
		foreach (var key in keys)
		{
			if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(ExtractString(value)))
			{
				return ExtractString(value);
			}
		}

		return null;
	}

	private static string? ExtractString(JsonElement value) => value.ValueKind switch
	{
		JsonValueKind.String => Normalize(value.GetString()),
		JsonValueKind.Number => value.ToString(),
		JsonValueKind.True => "true",
		JsonValueKind.False => "false",
		JsonValueKind.Array => Normalize(string.Join(", ", value.EnumerateArray().Select(ExtractString))),
		_ => null
	};

	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
