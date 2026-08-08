using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmRegistrationResponseValidator
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmRegistrationTemplateDto DeserializeTemplate(string json)
	{
		return JsonSerializer.Deserialize<AdmissionCrmRegistrationTemplateDto>(json, JsonOptions)
		       ?? throw new BusinessException("AdmissionCrm:InvalidStoredRegistrationConfiguration");
	}

	public static decimal Validate(
		AdmissionCrmRegistrationApplication application,
		AdmissionCrmRegistrationTemplateDto template,
		IReadOnlyDictionary<string, JsonElement> responses,
		bool requireComplete,
		bool allowStaffManagedFields = false)
	{
		var storedPrefill = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
			application.PrefillJson,
			JsonOptions) ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
		var prefill = new Dictionary<string, JsonElement>(
			storedPrefill,
			StringComparer.OrdinalIgnoreCase);
		var fields = template.Sections
			.Where(section => section.ParentVisible)
			.SelectMany(section => section.Fields)
			.ToList();
		var fieldKeys = fields.Select(field => field.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (responses.Keys.Any(key => !fieldKeys.Contains(key)))
		{
			throw new BusinessException("AdmissionCrm:UnknownRegistrationResponseField");
		}

		var visibleFields = fields
			.Where(field => IsVisible(field, responses, prefill))
			.ToList();
		var completed = 0;
		foreach (var field in visibleFields)
		{
			var value = GetValue(field, responses, prefill);
			if (!allowStaffManagedFields && !field.ParentEditable && responses.ContainsKey(field.Key))
			{
				throw new BusinessException("AdmissionCrm:RegistrationFieldIsNotParentEditable")
					.WithData("FieldKey", field.Key);
			}
			var required = field.Required || MatchesCondition(
				field.RequiredWhenFieldKey,
				field.RequiredWhenValue,
				responses,
				prefill,
				fields);
			if (IsBlank(value))
			{
				if (requireComplete && required)
				{
					throw new BusinessException("AdmissionCrm:RequiredRegistrationFieldMissing")
						.WithData("FieldKey", field.Key);
				}
				continue;
			}
			ValidateValue(field, value!.Value);
			completed++;
		}

		return visibleFields.Count == 0
			? 100
			: Math.Round((decimal)completed * 100 / visibleFields.Count, 2);
	}

	private static bool IsVisible(
		AdmissionCrmRegistrationFieldDto field,
		IReadOnlyDictionary<string, JsonElement> responses,
		IReadOnlyDictionary<string, JsonElement> prefill)
	{
		return string.IsNullOrWhiteSpace(field.VisibleWhenFieldKey) ||
		       MatchesCondition(
			       field.VisibleWhenFieldKey,
			       field.VisibleWhenValue,
			       responses,
			       prefill,
			       null);
	}

	private static bool MatchesCondition(
		string? fieldKey,
		string? expected,
		IReadOnlyDictionary<string, JsonElement> responses,
		IReadOnlyDictionary<string, JsonElement> prefill,
		IReadOnlyCollection<AdmissionCrmRegistrationFieldDto>? fields)
	{
		if (string.IsNullOrWhiteSpace(fieldKey)) return false;
		responses.TryGetValue(fieldKey, out var response);
		if (response.ValueKind != JsonValueKind.Undefined)
		{
			return string.Equals(JsonValueText(response), expected ?? "true", StringComparison.OrdinalIgnoreCase);
		}
		var conditionField = fields?.FirstOrDefault(field =>
			string.Equals(field.Key, fieldKey, StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrWhiteSpace(conditionField?.SystemField) &&
		    prefill.TryGetValue(conditionField.SystemField, out var prefilled))
		{
			return string.Equals(JsonValueText(prefilled), expected ?? "true", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static JsonElement? GetValue(
		AdmissionCrmRegistrationFieldDto field,
		IReadOnlyDictionary<string, JsonElement> responses,
		IReadOnlyDictionary<string, JsonElement> prefill)
	{
		if (responses.TryGetValue(field.Key, out var response)) return response;
		if (!string.IsNullOrWhiteSpace(field.SystemField) &&
		    prefill.TryGetValue(field.SystemField, out var value)) return value;
		return null;
	}

	private static bool IsBlank(JsonElement? value)
	{
		if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
		{
			return true;
		}
		return value.Value.ValueKind switch
		{
			JsonValueKind.String => string.IsNullOrWhiteSpace(value.Value.GetString()),
			JsonValueKind.Array => value.Value.GetArrayLength() == 0,
			_ => false
		};
	}

	private static void ValidateValue(AdmissionCrmRegistrationFieldDto field, JsonElement value)
	{
		var text = JsonValueText(value);
		if (field.MinimumLength.HasValue && text.Length < field.MinimumLength.Value ||
		    field.MaximumLength.HasValue && text.Length > field.MaximumLength.Value)
		{
			throw new BusinessException("AdmissionCrm:RegistrationFieldLengthInvalid")
				.WithData("FieldKey", field.Key);
		}
		if (!string.IsNullOrWhiteSpace(field.ValidationPattern) &&
		    !Regex.IsMatch(
			    text,
			    field.ValidationPattern,
			    RegexOptions.CultureInvariant,
			    TimeSpan.FromMilliseconds(250)))
		{
			throw new BusinessException("AdmissionCrm:RegistrationFieldPatternInvalid")
				.WithData("FieldKey", field.Key);
		}

		switch (field.FieldType)
		{
			case AdmissionCrmFormFieldType.Email:
				try { _ = new MailAddress(text); }
				catch (FormatException)
				{
					throw new BusinessException("AdmissionCrm:RegistrationEmailInvalid")
						.WithData("FieldKey", field.Key);
				}
				break;
			case AdmissionCrmFormFieldType.Phone when text.Count(char.IsDigit) < 5:
				throw new BusinessException("AdmissionCrm:RegistrationPhoneInvalid")
					.WithData("FieldKey", field.Key);
			case AdmissionCrmFormFieldType.Date when !DateTime.TryParse(
				text,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out _):
				throw new BusinessException("AdmissionCrm:RegistrationDateInvalid")
					.WithData("FieldKey", field.Key);
			case AdmissionCrmFormFieldType.Number:
				if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ||
				    field.MinimumValue.HasValue && number < field.MinimumValue.Value ||
				    field.MaximumValue.HasValue && number > field.MaximumValue.Value)
				{
					throw new BusinessException("AdmissionCrm:RegistrationNumberInvalid")
						.WithData("FieldKey", field.Key);
				}
				break;
			case AdmissionCrmFormFieldType.Select when field.Options.Count > 0 &&
			                                              !field.Options.Contains(text, StringComparer.OrdinalIgnoreCase):
				throw new BusinessException("AdmissionCrm:RegistrationOptionInvalid")
					.WithData("FieldKey", field.Key);
			case AdmissionCrmFormFieldType.MultiSelect when value.ValueKind != JsonValueKind.Array:
				throw new BusinessException("AdmissionCrm:RegistrationOptionInvalid")
					.WithData("FieldKey", field.Key);
		}
	}

	private static string JsonValueText(JsonElement value)
	{
		return value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: value.ToString();
	}
}
