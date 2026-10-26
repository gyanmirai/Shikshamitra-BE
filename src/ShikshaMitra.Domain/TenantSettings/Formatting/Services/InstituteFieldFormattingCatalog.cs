using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.Formatting;

public static class InstituteFieldFormattingCatalog
{
	public static readonly string[] Scopes = ["buildings", "roomTypes", "rooms", "shifts", "schoolIdentity", "schoolDocuments"];

	public static Dictionary<string, AcademicTextCase> Defaults(string scope) => Fields(scope)
		.ToDictionary(field => field, _ => AcademicTextCase.Default, StringComparer.Ordinal);

	private static string[] Fields(string scope) => scope switch
	{
		"buildings" => ["buildingName", "floorName", "description"],
		"roomTypes" => ["roomTypeName", "shortName"],
		"rooms" => ["roomName", "featureName"],
		"shifts" => ["shiftName"],
		"schoolIdentity" => ["principalName", "designation"],
		"schoolDocuments" => ["name", "notes"],
		_ => throw new AbpValidationException("Choose a supported Institute Setup form.")
	};

	public static void Validate(string scope, Dictionary<string, AcademicTextCase>? rules)
	{
		var fields = Fields(scope);
		if (rules == null || rules.Count != fields.Length || fields.Any(field => !rules.ContainsKey(field))
			|| rules.Values.Any(mode => !Enum.IsDefined(mode)))
			throw new AbpValidationException("Choose a supported text case for every field in this form.");
	}

	public static string? Format(IReadOnlyDictionary<string, AcademicTextCase> rules, string field, string? value) =>
		value == null ? null : AcademicNameFormatter.Format(value, rules[field]);

	public static string Required(IReadOnlyDictionary<string, AcademicTextCase> rules, string field, string value, int maxLength = int.MaxValue)
	{
		var formatted = Format(rules, field, value);
		if (string.IsNullOrWhiteSpace(formatted) || formatted.Length > maxLength)
			throw new AbpValidationException($"Enter {field} using at most {maxLength} characters after formatting.");
		return formatted;
	}

	public static string? Optional(IReadOnlyDictionary<string, AcademicTextCase> rules, string field, string? value, int maxLength)
	{
		var formatted = Format(rules, field, value);
		if (formatted?.Length > maxLength)
			throw new AbpValidationException($"Use at most {maxLength} characters for {field} after formatting.");
		return formatted;
	}
}
