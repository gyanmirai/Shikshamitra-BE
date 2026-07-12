using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmApplicantChecklistSerializer
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static string SerializeItems(IReadOnlyList<AdmissionCrmApplicantChecklistItemDto>? items)
	{
		var normalized = NormalizeItems(items).ToList();
		var json = JsonSerializer.Serialize(normalized, JsonOptions);
		if (json.Length > AdmissionCrmConsts.MaxRoutingRulesJsonLength)
		{
			throw new BusinessException("AdmissionCrm:ApplicantChecklistTooLarge");
		}

		return json;
	}

	public static List<AdmissionCrmApplicantChecklistItemDto> DeserializeItems(string? itemsJson)
	{
		if (string.IsNullOrWhiteSpace(itemsJson))
		{
			return [];
		}

		try
		{
			return NormalizeItems(
				JsonSerializer.Deserialize<List<AdmissionCrmApplicantChecklistItemDto>>(itemsJson, JsonOptions))
				.ToList();
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static IEnumerable<AdmissionCrmApplicantChecklistItemDto> NormalizeItems(
		IReadOnlyList<AdmissionCrmApplicantChecklistItemDto>? items)
	{
		if (items == null || items.Count == 0)
		{
			return [];
		}

		var normalized = items
			.Select((item, index) => NormalizeItem(item, index))
			.Where(item => !string.IsNullOrWhiteSpace(item.Label))
			.OrderBy(item => item.DisplayOrder)
			.ThenBy(item => item.Label)
			.ToList();
		var duplicateKey = normalized
			.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault(group => group.Count() > 1)
			?.Key;
		if (!string.IsNullOrWhiteSpace(duplicateKey))
		{
			throw new BusinessException("AdmissionCrm:DuplicateApplicantChecklistKey")
				.WithData("Key", duplicateKey);
		}

		return normalized;
	}

	private static AdmissionCrmApplicantChecklistItemDto NormalizeItem(
		AdmissionCrmApplicantChecklistItemDto item,
		int index)
	{
		var label = NormalizeRequired(
			item.Label,
			nameof(item.Label),
			AdmissionCrmConsts.MaxTimelineTitleLength);
		return new AdmissionCrmApplicantChecklistItemDto
		{
			Key = NormalizeKey(item.Key, label, index),
			Label = label,
			Details = NormalizeOptional(
				item.Details,
				nameof(item.Details),
				AdmissionCrmConsts.MaxDescriptionLength),
			IsRequired = item.IsRequired,
			DisplayOrder = item.DisplayOrder <= 0 ? (index + 1) * 10 : item.DisplayOrder
		};
	}

	private static string NormalizeKey(string? key, string label, int index)
	{
		var value = string.IsNullOrWhiteSpace(key)
			? $"{label}-{index + 1}"
			: key;
		var normalized = new string(value
			.Trim()
			.ToLowerInvariant()
			.Select(character => char.IsLetterOrDigit(character) ? character : '-')
			.ToArray());
		normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
		if (string.IsNullOrWhiteSpace(normalized))
		{
			normalized = $"item-{index + 1}";
		}

		return normalized.Length <= AdmissionCrmConsts.MaxPurposeLength
			? normalized
			: normalized[..AdmissionCrmConsts.MaxPurposeLength];
	}

	private static string NormalizeRequired(string value, string name, int maxLength)
	{
		return Check.NotNullOrWhiteSpace(value, name, maxLength).Trim();
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
