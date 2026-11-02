using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Compatibility for known academic questions without rewriting frozen form definitions.</summary>
internal static class AdmissionAcademicFormOptions
{
	internal static string SerializeOptions(IEnumerable<AdmissionFormOptionDto> options) => JsonSerializer.Serialize(options.Select(option =>
		new { value = option.Value, label = option.Label, displayOrder = option.DisplayOrder, isActive = option.IsActive,
			boardId = option.BoardId, boardLabel = option.BoardLabel }));

	internal static string? OptionsJson(string code, AdmissionFormFieldType type, string? configured)
	{
		if (type is not (AdmissionFormFieldType.Select or AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.Radio) ||
			AdmissionFormMasterDataValidator.TrySource(configured, out _, out _)) return configured;
		return code.ToUpperInvariant() switch
		{
			"CLASS_INTERESTED" or "CLASS_APPLYING_FOR" => "{\"source\":\"Standards\"}",
			"CLASS" => "{\"source\":\"StandardsByMedium\",\"parentField\":\"MEDIUM\"}",
			"DIVISION" => "{\"source\":\"DivisionsByStandard\",\"parentField\":\"CLASS\"}",
			_ => configured
		};
	}

	internal static bool IsAcademic(string key) => key.Equals("Standards", StringComparison.OrdinalIgnoreCase) ||
		key.Equals("StandardsByMedium", StringComparison.OrdinalIgnoreCase) || key.Equals("StandardsByMediumStage", StringComparison.OrdinalIgnoreCase) ||
		key.Equals("DivisionsByStandard", StringComparison.OrdinalIgnoreCase);
}
