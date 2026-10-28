using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationFormValidator(AdmissionWorkflowConditionEvaluator conditionEvaluator)
	: ITransientDependency
{
	public AdmissionFormValidationResult Validate(
		IEnumerable<AdmissionFormFieldDefinition> fields,
		IEnumerable<AdmissionFormSectionDefinition> sections,
		IReadOnlyDictionary<string, string> values)
		=> Validate(fields, sections, values, null);

	internal AdmissionFormValidationResult Validate(
		IEnumerable<AdmissionFormFieldDefinition> fields,
		IEnumerable<AdmissionFormSectionDefinition> sections,
		IReadOnlyDictionary<string, string> values,
		AdmissionFormExperienceMetadata? experience)
	{
		var sectionList = sections.ToList();
		var fieldList = fields.ToList();
		var sectionRules = sectionList.ToDictionary(section => section.Id, section => section.VisibilityRuleJson);
		var errors = new List<string>();
		var fieldErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var section in sectionList)
		{
			var sectionFields = fieldList.Where(field => field.SectionId == section.Id).ToList();
			var repeatable = experience?.Sections.GetValueOrDefault(section.Code);
			if (repeatable?.IsRepeatable == true)
			{
				ValidateRepeatableSection(section, sectionFields, repeatable, values, errors, fieldErrors);
				continue;
			}
			foreach (var field in sectionFields) ValidateField(field, sectionRules.GetValueOrDefault(field.SectionId), values, errors, fieldErrors);
		}
		return new AdmissionFormValidationResult(errors, fieldErrors);
	}

	private void ValidateRepeatableSection(
		AdmissionFormSectionDefinition section,
		IReadOnlyCollection<AdmissionFormFieldDefinition> fields,
		AdmissionFormSectionExperienceMetadata repeatable,
		IReadOnlyDictionary<string, string> values,
		ICollection<string> errors,
		IDictionary<string, string> fieldErrors)
	{
		var rows = fields.ToDictionary(
			field => field.Code,
			field => ReadArray(values.GetValueOrDefault(field.Code)),
			StringComparer.OrdinalIgnoreCase);
		var count = rows.Count == 0 ? 0 : rows.Values.Max(items => items.Count);
		if (count < repeatable.MinEntries)
		{
			var message = $"{section.Code} requires at least {repeatable.MinEntries} entries.";
			errors.Add(message);
			fieldErrors.TryAdd(section.Code, message);
		}
		if (count > repeatable.MaxEntries)
		{
			var message = $"{section.Code} allows at most {repeatable.MaxEntries} entries.";
			errors.Add(message);
			fieldErrors.TryAdd(section.Code, message);
		}
		for (var index = 0; index < count; index++)
		{
			var rowValues = values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
			foreach (var field in fields) rowValues[field.Code] = rows[field.Code].ElementAtOrDefault(index) ?? string.Empty;
			foreach (var field in fields)
			{
				var key = $"{field.Code}[{index}]";
				ValidateField(field, section.VisibilityRuleJson, rowValues, errors, fieldErrors, key);
			}
		}
	}

	private void ValidateField(
		AdmissionFormFieldDefinition field,
		string? sectionRule,
		IReadOnlyDictionary<string, string> values,
		ICollection<string> errors,
		IDictionary<string, string> fieldErrors,
		string? errorKey = null)
	{
		if (field.FieldType == AdmissionFormFieldType.Static) return;
		var visible = IsVisible(field.VisibilityRuleJson, values) && IsVisible(sectionRule, values);
		var value = values.GetValueOrDefault(field.Code, string.Empty);
		var conditionallyRequired = visible && !string.IsNullOrWhiteSpace(field.RequiredRuleJson) && IsVisible(field.RequiredRuleJson, values);
		if (visible && (field.Required || conditionallyRequired) && string.IsNullOrWhiteSpace(value))
		{
			var message = Message(field, "required", $"{field.Code} is required.");
			errors.Add(message);
			fieldErrors.TryAdd(errorKey ?? field.Code, message);
		}
		if (!visible || string.IsNullOrWhiteSpace(value)) return;
		foreach (var error in ValidateValue(field, value))
		{
			errors.Add(error);
			fieldErrors.TryAdd(errorKey ?? field.Code, error);
		}
	}

	private static List<string> ReadArray(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return [];
		try
		{
			using var document = JsonDocument.Parse(value);
			return document.RootElement.ValueKind == JsonValueKind.Array
				? document.RootElement.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString()).ToList()
				: [];
		}
		catch (JsonException) { return []; }
	}

	public IReadOnlySet<string> GetVisibleFieldCodes(
		IEnumerable<AdmissionFormFieldDefinition> fields,
		IEnumerable<AdmissionFormSectionDefinition> sections,
		IReadOnlyDictionary<string, string> values)
	{
		var sectionRules = sections.ToDictionary(section => section.Id, section => section.VisibilityRuleJson);
		return fields
			.Where(field => IsVisible(field.VisibilityRuleJson, values) && IsVisible(sectionRules.GetValueOrDefault(field.SectionId), values))
			.Select(field => field.Code)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private bool IsVisible(string? ruleJson, IReadOnlyDictionary<string, string> values)
	{
		try
		{
			return conditionEvaluator.Evaluate(ruleJson, values);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static IEnumerable<string> ValidateValue(AdmissionFormFieldDefinition field, string value)
	{
		var errors = new List<string>();
		if (field.BindingType == AdmissionFormBindingType.System)
		{
			if (field.BindingKey == "Identity.AadhaarNumber" && !Regex.IsMatch(value, @"^[2-9][0-9]{11}$"))
				errors.Add($"{field.Code} must contain a valid 12-digit Aadhaar number.");
			if (field.BindingKey == "Identity.PanNumber" && !Regex.IsMatch(value, @"^[A-Z]{5}[0-9]{4}[A-Z]$"))
				errors.Add($"{field.Code} must contain a valid PAN number.");
			if (field.BindingKey == "Applicant.ProfilePhoto")
			{
				try { AdmissionStudentPhoto.Decode(value); }
				catch (Volo.Abp.BusinessException) { errors.Add($"{field.Code} must be a JPEG or PNG photo up to 2 MB."); }
			}
		}
		if (field.FieldType == AdmissionFormFieldType.Number && !decimal.TryParse(value, out _))
			errors.Add($"{field.Code} must be a number.");
		if (field.FieldType == AdmissionFormFieldType.Date && !DateTimeOffset.TryParse(value, out _))
			errors.Add($"{field.Code} must be a valid date.");
		if (field.FieldType == AdmissionFormFieldType.Email && !IsEmail(value))
			errors.Add($"{field.Code} must be a valid email address.");
		if (field.FieldType == AdmissionFormFieldType.Checkbox &&
			!bool.TryParse(value, out _))
			errors.Add($"{field.Code} must be true or false.");
		if (field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio)
			ValidateSingleOption(field, value, errors);
		if (field.FieldType == AdmissionFormFieldType.MultiSelect)
			ValidateMultipleOptions(field, value, errors);
		if (field.FieldType == AdmissionFormFieldType.AddressGroup)
			ValidateJsonObject(field, value, errors);
		if (string.IsNullOrWhiteSpace(field.ValidationJson)) return errors;
		try
		{
			using var document = JsonDocument.Parse(field.ValidationJson);
			var rule = document.RootElement;
			if (rule.TryGetProperty("minLength", out var min) && value.Length < min.GetInt32())
				errors.Add(Message(field, "minLength", $"{field.Code} is too short."));
			if (rule.TryGetProperty("maxLength", out var max) && value.Length > max.GetInt32())
				errors.Add(Message(field, "maxLength", $"{field.Code} is too long."));
			if (rule.TryGetProperty("min", out var minimum) &&
				(!decimal.TryParse(value, out var number) || number < minimum.GetDecimal()))
				errors.Add(Message(field, "min", $"{field.Code} is below the minimum value."));
			if (rule.TryGetProperty("max", out var maximum) &&
				(!decimal.TryParse(value, out var maximumNumber) || maximumNumber > maximum.GetDecimal()))
				errors.Add(Message(field, "max", $"{field.Code} exceeds the maximum value."));
			if (rule.TryGetProperty("regex", out var regex) &&
				!Regex.IsMatch(value, regex.GetString() ?? string.Empty))
				errors.Add(Message(field, "regex", $"{field.Code} has an invalid format."));
			if (rule.TryGetProperty("email", out var email) && email.GetBoolean() && !IsEmail(value))
				errors.Add(Message(field, "email", $"{field.Code} must be a valid email."));
			if (((rule.TryGetProperty("phone", out var phone) && phone.GetBoolean()) ||
				field.FieldType == AdmissionFormFieldType.Phone) && !Regex.IsMatch(value, @"^\+?[0-9 ()-]{7,20}$"))
				errors.Add(Message(field, "phone", $"{field.Code} must be a valid phone number."));
			if (rule.TryGetProperty("format", out var format) &&
				new[] { "country", "state", "city" }.Contains(format.GetString(), StringComparer.OrdinalIgnoreCase))
				ValidateLocationValue(field, value, errors);
			ValidateDate(field, value, rule, errors);
		}
		catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
		{
			errors.Add($"{field.Code} has invalid validation configuration.");
		}
		return errors;
	}

	private static void ValidateLocationValue(AdmissionFormFieldDefinition field, string value, ICollection<string> errors)
	{
		try
		{
			using var document = JsonDocument.Parse(value);
			if (document.RootElement.ValueKind != JsonValueKind.Object ||
				!document.RootElement.TryGetProperty("name", out var name) ||
				name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()))
				errors.Add($"{field.Code} must contain a valid location.");
		}
		catch (JsonException) { errors.Add($"{field.Code} must contain a valid location."); }
	}

	private static void ValidateSingleOption(AdmissionFormFieldDefinition field, string value, ICollection<string> errors)
	{
		if (AdmissionFormMasterDataValidator.TrySource(AdmissionAcademicFormOptions.OptionsJson(field.Code, field.FieldType, field.OptionsJson), out var key, out _) && AdmissionAcademicFormOptions.IsAcademic(key)) return;
		var options = ReadOptions(field);
		if (options.Count > 0 && !options.Contains(value, StringComparer.OrdinalIgnoreCase))
			errors.Add($"{field.Code} contains an invalid option.");
	}

	private static void ValidateMultipleOptions(AdmissionFormFieldDefinition field, string value, ICollection<string> errors)
	{
		try
		{
			using var document = JsonDocument.Parse(value);
			if (document.RootElement.ValueKind != JsonValueKind.Array)
			{
				errors.Add($"{field.Code} must contain multiple options.");
				return;
			}
			var options = ReadOptions(field);
			var dynamicOptions = false;
			var effectiveOptions = AdmissionAcademicFormOptions.OptionsJson(field.Code, field.FieldType, field.OptionsJson);
			if (!string.IsNullOrWhiteSpace(effectiveOptions))
			{
				using var source = JsonDocument.Parse(effectiveOptions);
				dynamicOptions = source.RootElement.ValueKind == JsonValueKind.Object && source.RootElement.TryGetProperty("source", out _);
			}
			foreach (var item in document.RootElement.EnumerateArray())
				if (item.ValueKind != JsonValueKind.String || (!dynamicOptions && !options.Contains(item.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase)))
					errors.Add($"{field.Code} contains an invalid option.");
		}
		catch (JsonException) { errors.Add($"{field.Code} must contain multiple options."); }
	}

	private static List<string> ReadOptions(AdmissionFormFieldDefinition field)
	{
		var result = new List<string>();
		if (string.IsNullOrWhiteSpace(field.OptionsJson)) return result;
		try
		{
			using var document = JsonDocument.Parse(field.OptionsJson);
			if (document.RootElement.ValueKind != JsonValueKind.Array) return result;
			foreach (var item in document.RootElement.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.String) result.Add(item.GetString() ?? string.Empty);
				else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var value)) result.Add(value.ToString());
			}
		}
		catch (JsonException) { }
		return result;
	}

	private static void ValidateJsonObject(AdmissionFormFieldDefinition field, string value, ICollection<string> errors)
	{
		try
		{
			using var document = JsonDocument.Parse(value);
			if (document.RootElement.ValueKind != JsonValueKind.Object) errors.Add($"{field.Code} must contain an address object.");
		}
		catch (JsonException) { errors.Add($"{field.Code} must contain an address object."); }
	}

	private static void ValidateDate(
		AdmissionFormFieldDefinition field,
		string value,
		JsonElement rule,
		ICollection<string> errors)
	{
		if (field.FieldType != AdmissionFormFieldType.Date || !DateTime.TryParse(value, out var date)) return;
		if (rule.TryGetProperty("minDate", out var minDate) && date.Date < minDate.GetDateTime().Date)
			errors.Add(Message(field, "minDate", $"{field.Code} is before the allowed date."));
		if (rule.TryGetProperty("maxDate", out var maxDate) && date.Date > maxDate.GetDateTime().Date)
			errors.Add(Message(field, "maxDate", $"{field.Code} is after the allowed date."));
	}

	private static bool IsEmail(string value)
	{
		try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
		catch (FormatException) { return false; }
	}

	private static void AddError(
		AdmissionFormFieldDefinition field,
		string rule,
		string fallback,
		ICollection<string> errors,
		IDictionary<string, string> fieldErrors)
	{
		var message = Message(field, rule, fallback);
		errors.Add(message);
		fieldErrors.TryAdd(field.Code, message);
	}

	private static string Message(AdmissionFormFieldDefinition field, string rule, string fallback)
	{
		if (string.IsNullOrWhiteSpace(field.ValidationJson)) return fallback;
		try
		{
			using var document = JsonDocument.Parse(field.ValidationJson);
			var root = document.RootElement;
			if (root.TryGetProperty("messages", out var messages) &&
				messages.TryGetProperty(rule, out var configured)) return configured.GetString() ?? fallback;
			if (root.TryGetProperty("message", out var message)) return message.GetString() ?? fallback;
		}
		catch (JsonException) { }
		return fallback;
	}
}

public sealed record AdmissionFormValidationResult(
	List<string> Errors,
	Dictionary<string, string> FieldErrors);
