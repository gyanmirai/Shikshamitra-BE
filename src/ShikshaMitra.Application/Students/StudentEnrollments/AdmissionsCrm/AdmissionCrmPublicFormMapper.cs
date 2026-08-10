using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPublicFormMapper
{
	internal static PublicAdmissionInquiryFormDto ToDto(
		Guid tenantId,
		AdmissionInquiryFormTemplate template,
		string? requestedLanguage,
		IReadOnlyList<string> supportedLanguages)
	{
		var language = AdmissionCrmPublicBranding.NormalizeLanguage(requestedLanguage, supportedLanguages);
		var languageFallbacks = BuildLanguageFallbacks(language);
		var localizations = JsonSerializer.Deserialize<Dictionary<string, AdmissionCrmFormTemplateLocalizationDto>>(
			template.LocalizationsJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];
		var localization = languageFallbacks
			.Select(candidate => localizations.GetValueOrDefault(candidate))
			.FirstOrDefault(candidate => candidate != null);
		var fields = DeserializeFields(template.FieldsJson)
			.Select(field => LocalizeField(field, languageFallbacks))
			.ToList();
		return new PublicAdmissionInquiryFormDto
		{
			TenantId = tenantId,
			TemplateId = template.Id,
			AcademicYearId = template.AcademicYearId,
			TemplateName = localization?.TemplateName ?? template.TemplateName,
			Description = localization?.Description ?? template.Description,
			Channel = template.Channel,
			ConsentText = localization?.ConsentText ?? template.ConsentText ?? string.Empty,
			PrivacyNoticeUrl = template.PrivacyNoticeUrl ?? string.Empty,
			ConsentVersion = template.ConcurrencyStamp,
			Fields = fields
		};
	}

	internal static IReadOnlyList<AdmissionCrmFormFieldDto> DeserializeFields(string fieldsJson) =>
		JsonSerializer.Deserialize<List<AdmissionCrmFormFieldDto>>(
			fieldsJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];

	private static AdmissionCrmFormFieldDto LocalizeField(
		AdmissionCrmFormFieldDto field,
		IReadOnlyList<string> languageFallbacks)
	{
		field.Label = GetLocalizedValue(field.LocalizedLabels, languageFallbacks) ?? field.Label;
		field.HelperText = GetLocalizedValue(field.LocalizedHelperTexts, languageFallbacks) ?? field.HelperText;
		field.Placeholder = GetLocalizedValue(field.LocalizedPlaceholders, languageFallbacks) ?? field.Placeholder;
		field.OptionLabels = languageFallbacks
			.Select(candidate => field.LocalizedOptions.GetValueOrDefault(candidate))
			.FirstOrDefault(optionLabels => optionLabels?.Count == field.Options.Count) ?? field.Options;
		return field;
	}

	private static IReadOnlyList<string> BuildLanguageFallbacks(string language)
	{
		var baseLanguage = language.Split('-', 2)[0];
		return baseLanguage == language ? [language] : [language, baseLanguage];
	}

	private static string? GetLocalizedValue(
		IReadOnlyDictionary<string, string> values,
		IReadOnlyList<string> languageFallbacks) =>
		languageFallbacks.Select(values.GetValueOrDefault).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
