using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Shared source membership validation for submission and governed captured-detail corrections.</summary>
public class AdmissionFormMasterDataValidator(AdmissionFormDataSourceRegistry sources, AdmissionApplicationFormValidator validator) : ITransientDependency
{
	internal async Task ValidateAsync(IEnumerable<AdmissionFormFieldDefinition> fields, IEnumerable<AdmissionFormSectionDefinition> sections,
		IReadOnlyDictionary<string, string> values, Guid academicYearId, AdmissionFormValidationResult validation, AdmissionFormExperienceMetadata experience)
	{
		var sectionRows = sections.ToList();
		foreach (var section in sectionRows)
		{
			var sectionFields = fields.Where(x => x.SectionId == section.Id).ToList();
			var repeated = experience.Sections.GetValueOrDefault(section.Code)?.IsRepeatable == true;
			var count = repeated ? sectionFields.Select(f => ReadMultiple(values.GetValueOrDefault(f.Code)).Count).DefaultIfEmpty(0).Max() : 1;
			for (var index = 0; index < count; index++)
			{
				var row = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
				if (repeated)
					foreach (var field in sectionFields) row[field.Code] = ReadMultiple(values.GetValueOrDefault(field.Code)).ElementAtOrDefault(index) ?? "";
				var visible = validator.GetVisibleFieldCodes(sectionFields, [section], row);
				foreach (var field in sectionFields.Where(f => visible.Contains(f.Code) && f.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.Radio))
				{
					if (!TrySource(AdmissionAcademicFormOptions.OptionsJson(field.Code, field.FieldType, field.OptionsJson), out var sourceKey, out var parentField)) continue;
					var selected = field.FieldType == AdmissionFormFieldType.MultiSelect ? ReadMultiple(row.GetValueOrDefault(field.Code)) : [row.GetValueOrDefault(field.Code) ?? ""];
					var valid = true;
					if (sourceKey == "StudentSiblings")
					{
						try { valid = await sources.AreSiblingIdsAllowedAsync(AdmissionStudentProfileMapping.ReadSiblings(row.GetValueOrDefault(field.Code)) ?? []); }
						catch (BusinessException) { valid = false; }
					}
					else
					{
						var parent = parentField != null && Guid.TryParse(row.GetValueOrDefault(parentField), out var parentId) ? parentId : (Guid?)null;
						var allowed = (await sources.GetOptionsAsync(sourceKey, parent, academicYearId)).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
						valid = selected.All(x => string.IsNullOrWhiteSpace(x) || allowed.Contains(x));
					}
					if (valid) continue;
					var key = repeated ? $"{field.Code}[{index}]" : field.Code;
					var message = $"{field.Code} contains an invalid option.";
					validation.Errors.Add(message); validation.FieldErrors.TryAdd(key, message);
				}
			}
		}
	}
	internal static bool TrySource(string? json, out string key, out string? parent)
	{
		key = ""; parent = null;
		if (string.IsNullOrWhiteSpace(json)) return false;
		try
		{
			using var parsed = JsonDocument.Parse(json);
			if (parsed.RootElement.ValueKind != JsonValueKind.Object || !parsed.RootElement.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.String) return false;
			key = source.GetString() ?? "";
			if (parsed.RootElement.TryGetProperty("parentField", out var property) && property.ValueKind == JsonValueKind.String) parent = property.GetString();
			return key.Length > 0;
		}
		catch (JsonException) { return false; }
	}
	private static IReadOnlyList<string> ReadMultiple(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return [];
		try
		{
			using var parsed = JsonDocument.Parse(value);
			return parsed.RootElement.ValueKind == JsonValueKind.Array ? parsed.RootElement.EnumerateArray()
				.Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.GetRawText()).ToList() : [];
		}
		catch (JsonException) { return []; }
	}
}
