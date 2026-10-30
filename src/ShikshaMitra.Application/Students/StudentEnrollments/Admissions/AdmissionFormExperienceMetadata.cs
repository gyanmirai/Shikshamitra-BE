using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal sealed class AdmissionFormExperienceMetadata
{
	public string DefaultLanguage { get; set; } = "en";
	public List<string> SupportedLanguages { get; set; } = ["en"];
	public Dictionary<string, AdmissionFormSectionExperienceMetadata> Sections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class AdmissionFormSectionExperienceMetadata
{
	public bool IsRepeatable { get; set; }
	public int MinEntries { get; set; } = 1;
	public int MaxEntries { get; set; } = 1;
	public string? EntryLabel { get; set; }
	public Dictionary<string, AdmissionFormTranslationDto> Translations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, Dictionary<string, AdmissionFormTranslationDto>> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class AdmissionFormMetadataStore
{
	private const string ExperienceKey = "AdmissionFormExperience";
	private const string AuditKey = "AdmissionFormAudit";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionFormExperienceMetadata ReadExperience(AdmissionFormDefinition form) =>
		Deserialize(form.GetProperty<string>(ExperienceKey), new AdmissionFormExperienceMetadata());

	public static void WriteExperience(AdmissionFormDefinition form, CreateUpdateAdmissionFormDto input)
	{
		AdmissionFormInheritanceMetadataStore.Write(form, input);
		var languages = input.SupportedLanguages
			.Where(language => !string.IsNullOrWhiteSpace(language))
			.Select(language => language.Trim().ToLowerInvariant())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		var defaultLanguage = string.IsNullOrWhiteSpace(input.DefaultLanguage)
			? "en"
			: input.DefaultLanguage.Trim().ToLowerInvariant();
		if (!languages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase)) languages.Insert(0, defaultLanguage);
		if (languages.Count == 0) languages.Add("en");

		var metadata = new AdmissionFormExperienceMetadata
		{
			DefaultLanguage = defaultLanguage,
			SupportedLanguages = languages,
			Sections = input.Sections
				.GroupBy(section => section.Code.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
				.ToDictionary(
				group => group.Key,
				group =>
				{
					var section = group.Last();
					return new AdmissionFormSectionExperienceMetadata
					{
					IsRepeatable = section.IsRepeatable,
					MinEntries = section.IsRepeatable ? Math.Max(0, section.MinEntries) : 1,
					MaxEntries = section.IsRepeatable ? Math.Max(section.MinEntries, section.MaxEntries) : 1,
					EntryLabel = string.IsNullOrWhiteSpace(section.EntryLabel) ? null : section.EntryLabel.Trim(),
					Translations = NormalizeTranslations(section.Translations, languages),
					Fields = section.Fields
						.GroupBy(field => field.Code.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
						.ToDictionary(
						group => group.Key,
						group => NormalizeTranslations(group.Last().Translations, languages),
						StringComparer.OrdinalIgnoreCase)
					};
				},
				StringComparer.OrdinalIgnoreCase)
		};
		form.SetProperty(ExperienceKey, JsonSerializer.Serialize(metadata, JsonOptions));
	}

	public static void CopyExperience(AdmissionFormDefinition source, AdmissionFormDefinition target)
	{
		AdmissionFormInheritanceMetadataStore.Copy(source, target);
		var json = source.GetProperty<string>(ExperienceKey);
		if (!string.IsNullOrWhiteSpace(json)) target.SetProperty(ExperienceKey, json);
	}

	public static List<AdmissionFormAuditEntryDto> ReadAudit(AdmissionFormDefinition form) =>
		Deserialize(form.GetProperty<string>(AuditKey), new List<AdmissionFormAuditEntryDto>());

	public static void AddAudit(AdmissionFormDefinition form, string action, Guid? actorId, string? comment = null)
	{
		var audit = ReadAudit(form);
		audit.Add(new AdmissionFormAuditEntryDto
		{
			Id = Guid.NewGuid(),
			Action = action,
			ActorId = actorId,
			OccurredAt = DateTime.UtcNow,
			Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
			Version = form.Version
		});
		form.SetProperty(AuditKey, JsonSerializer.Serialize(audit.TakeLast(100), JsonOptions));
	}

	private static Dictionary<string, AdmissionFormTranslationDto> NormalizeTranslations(
		Dictionary<string, AdmissionFormTranslationDto>? translations,
		IReadOnlyCollection<string> languages) =>
		(translations ?? new Dictionary<string, AdmissionFormTranslationDto>())
			.Where(item => languages.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
			.ToDictionary(item => item.Key.Trim().ToLowerInvariant(), item => item.Value, StringComparer.OrdinalIgnoreCase);

	private static T Deserialize<T>(string? json, T fallback)
	{
		if (string.IsNullOrWhiteSpace(json)) return fallback;
		try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback; }
		catch (JsonException) { return fallback; }
	}
}
