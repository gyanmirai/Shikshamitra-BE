using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmRegistrationTemplateValidator
{
	private static readonly HashSet<string> SupportedSystemFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"firstName", "middleName", "lastName", "dateOfBirth", "parentGuardianName", "primaryContactNo", "email",
		"desiredClassName", "campus", "board", "medium", "admissionRound", "applicantCategory", "admissionRoute"
	};

	public static void NormalizeAndValidate(AdmissionCrmRegistrationTemplateUpsertDto input)
	{
		input.Name = Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name), AdmissionCrmConsts.MaxTemplateNameLength).Trim();
		if (input.IsFallback && AdmissionCrmPlaybookApplicability.Specificity(input.Applicability) > 0)
			throw new BusinessException("AdmissionCrm:FallbackRegistrationTemplateCannotHaveApplicability");
		if (input.Sections.Count == 0) throw new BusinessException("AdmissionCrm:RegistrationTemplateRequiresSection");
		var sectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var section in input.Sections)
		{
			section.Key = Check.NotNullOrWhiteSpace(section.Key, nameof(section.Key), 80).Trim();
			section.Title = Check.NotNullOrWhiteSpace(section.Title, nameof(section.Title), 120).Trim();
			section.LocalizedTitles = NormalizeLocalizations(section.LocalizedTitles, 120);
			section.LocalizedDescriptions = NormalizeLocalizations(section.LocalizedDescriptions, 500);
			if (!sectionKeys.Add(section.Key) || section.Fields.Count == 0)
				throw new BusinessException("AdmissionCrm:InvalidRegistrationTemplateSection").WithData("SectionKey", section.Key);
			foreach (var field in section.Fields) NormalizeField(field, fieldKeys);
		}
		foreach (var field in input.Sections.SelectMany(x => x.Fields))
		{
			if ((!string.IsNullOrWhiteSpace(field.VisibleWhenFieldKey) && !fieldKeys.Contains(field.VisibleWhenFieldKey)) ||
			    (!string.IsNullOrWhiteSpace(field.RequiredWhenFieldKey) && !fieldKeys.Contains(field.RequiredWhenFieldKey)))
				throw new BusinessException("AdmissionCrm:RegistrationFieldConditionTargetNotFound").WithData("FieldKey", field.Key);
		}
	}

	private static void NormalizeField(AdmissionCrmRegistrationFieldDto field, HashSet<string> fieldKeys)
	{
		field.Key = Check.NotNullOrWhiteSpace(field.Key, nameof(field.Key), 80).Trim();
		field.Label = Check.NotNullOrWhiteSpace(field.Label, nameof(field.Label), 120).Trim();
		if (!fieldKeys.Add(field.Key)) throw new BusinessException("AdmissionCrm:DuplicateRegistrationFieldKey").WithData("FieldKey", field.Key);
		if (!string.IsNullOrWhiteSpace(field.SystemField) && !SupportedSystemFields.Contains(field.SystemField))
			throw new BusinessException("AdmissionCrm:UnsupportedRegistrationSystemField").WithData("SystemField", field.SystemField);
		if (!Enum.IsDefined(field.FieldType)) throw new BusinessException("AdmissionCrm:InvalidRegistrationFieldType").WithData("FieldKey", field.Key);
		var requiresOptions = field.FieldType is AdmissionCrmFormFieldType.Select or AdmissionCrmFormFieldType.MultiSelect;
		if ((requiresOptions && field.Options.Count == 0) || (!requiresOptions && field.Options.Count > 0))
			throw new BusinessException("AdmissionCrm:InvalidRegistrationFieldOptions").WithData("FieldKey", field.Key);
		field.LocalizedLabels = NormalizeLocalizations(field.LocalizedLabels, 120);
		field.LocalizedHelpTexts = NormalizeLocalizations(field.LocalizedHelpTexts, 300);
		field.LocalizedOptions = NormalizeLocalizedOptions(field);
		if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
		{
			try { _ = new Regex(field.ValidationPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)); }
			catch (ArgumentException) { throw new BusinessException("AdmissionCrm:InvalidRegistrationValidationPattern").WithData("FieldKey", field.Key); }
		}
		if (field.MinimumLength > field.MaximumLength || field.MinimumValue > field.MaximumValue)
			throw new BusinessException("AdmissionCrm:InvalidRegistrationFieldValidation").WithData("FieldKey", field.Key);
	}

	private static IReadOnlyDictionary<string, string> NormalizeLocalizations(IReadOnlyDictionary<string, string> values, int max)
	{
		var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (language, value) in values)
			normalized[Check.NotNullOrWhiteSpace(language, nameof(language), 16).Trim().ToLowerInvariant()] =
				Check.NotNullOrWhiteSpace(value, nameof(value), max).Trim();
		return normalized;
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizeLocalizedOptions(AdmissionCrmRegistrationFieldDto field)
	{
		var normalized = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var (language, values) in field.LocalizedOptions)
		{
			var key = Check.NotNullOrWhiteSpace(language, nameof(language), 16).Trim().ToLowerInvariant();
			var items = values.Select(value => Check.NotNullOrWhiteSpace(value, nameof(value), 160).Trim()).ToList();
			if (items.Count != field.Options.Count) throw new BusinessException("AdmissionCrm:RegistrationLocalizedOptionsCountMismatch")
				.WithData("FieldKey", field.Key).WithData("Language", key);
			normalized[key] = items;
		}
		return normalized;
	}
}
