using System;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationCloneMetadata
{
	public static bool TryRead(AdmissionCrmCycleConfiguration? cycle, out Guid sourceYearId, out string fingerprint)
	{
		sourceYearId = Guid.Empty;
		fingerprint = string.Empty;
		if (cycle == null || !cycle.ExtraProperties.TryGetValue(
			AdmissionCrmConfigurationCloneWriter.CloneSourceAcademicYearProperty, out var sourceValue) ||
			!cycle.ExtraProperties.TryGetValue(
				AdmissionCrmConfigurationCloneWriter.ClonePreviewFingerprintProperty, out var fingerprintValue)) return false;
		fingerprint = Text(fingerprintValue);
		return Guid.TryParse(Text(sourceValue), out sourceYearId) && !string.IsNullOrWhiteSpace(fingerprint);
	}

	public static void ValidateYearPair(Guid sourceYearId, Guid targetYearId)
	{
		if (sourceYearId == Guid.Empty || targetYearId == Guid.Empty)
			throw new BusinessException("AdmissionCrm:AcademicYearRequired");
		if (sourceYearId == targetYearId)
			throw new BusinessException("AdmissionCrm:ConfigurationCloneDistinctYearsRequired");
	}

	public static string ConflictCode(AdmissionCrmConfigurationClonePreviewDto preview) =>
		preview.Conflicts.Any(x => x.Contains("source-year", StringComparison.OrdinalIgnoreCase) ||
			x.Contains("does not belong to the source academic year", StringComparison.OrdinalIgnoreCase))
			? "AdmissionCrm:ConfigurationCloneHierarchyMappingRequired" : "AdmissionCrm:ConfigurationCloneTargetNotEmpty";

	private static string Text(object? value) => value switch
	{
		null => string.Empty,
		JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
		_ => Convert.ToString(value) ?? string.Empty
	};
}
