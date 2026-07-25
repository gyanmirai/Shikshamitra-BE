using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal sealed record AdmissionCrmRouteEvidenceValidationResult(
	bool IsValid,
	IReadOnlyList<string> MissingCodes,
	IReadOnlyList<string> InvalidReferences);

internal static class AdmissionCrmRouteEvidenceValidator
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmRouteEvidenceValidationResult Validate(
		string configurationJson,
		string evidenceJson)
	{
		var evidenceReferences = JsonSerializer.Deserialize<IReadOnlyList<string>>(
			evidenceJson,
			JsonOptions) ?? [];
		return Validate(configurationJson, evidenceReferences);
	}

	public static AdmissionCrmRouteEvidenceValidationResult Validate(
		string configurationJson,
		IReadOnlyList<string> evidenceReferences)
	{
		var configuration = JsonSerializer.Deserialize<AdmissionCrmRouteConfigurationDto>(
			configurationJson,
			JsonOptions) ?? new AdmissionCrmRouteConfigurationDto();
		var requiredCodes = (configuration.RequiredEvidenceCodes ?? [])
			.Where(code => !string.IsNullOrWhiteSpace(code))
			.Select(NormalizeCode)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		var suppliedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var invalidReferences = new List<string>();
		foreach (var rawReference in evidenceReferences ?? [])
		{
			if (!TryParseReference(rawReference, out var code))
			{
				invalidReferences.Add(rawReference ?? string.Empty);
				continue;
			}

			suppliedCodes.Add(code);
		}

		var missingCodes = requiredCodes
			.Where(code => !suppliedCodes.Contains(code))
			.ToList();
		return new AdmissionCrmRouteEvidenceValidationResult(
			missingCodes.Count == 0 && invalidReferences.Count == 0,
			missingCodes,
			invalidReferences);
	}

	public static void EnsureValid(
		AdmissionCrmRouteDefinition definition,
		IReadOnlyList<string> evidenceReferences)
	{
		var result = Validate(definition.ConfigurationJson, evidenceReferences);
		if (result.InvalidReferences.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:RouteEvidenceReferenceInvalid")
				.WithData("RouteDefinitionId", definition.Id)
				.WithData("InvalidReferences", string.Join(", ", result.InvalidReferences));
		}

		if (result.MissingCodes.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:RequiredRouteEvidenceMissing")
				.WithData("RouteDefinitionId", definition.Id)
				.WithData("MissingEvidenceCodes", string.Join(", ", result.MissingCodes));
		}
	}

	private static bool TryParseReference(string? value, out string code)
	{
		code = string.Empty;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		var separator = value.IndexOf(':');
		if (separator <= 0 || separator == value.Length - 1)
		{
			return false;
		}

		var candidate = value[..separator].Trim();
		var reference = value[(separator + 1)..].Trim();
		if (candidate.Length == 0 ||
		    reference.Length == 0 ||
		    candidate.Any(character =>
			    !char.IsLetterOrDigit(character) &&
			    character != '_' &&
			    character != '-'))
		{
			return false;
		}

		code = NormalizeCode(candidate);
		return true;
	}

	private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
}
