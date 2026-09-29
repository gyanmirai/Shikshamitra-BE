using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>The persisted rule document is shared by configuration and runtime, not a UI-only preview.</summary>
public sealed class OnboardingRuleConfiguration
{
	public JsonElement Root { get; }
	public OnboardingRuleConfiguration(string json)
	{
		try
		{
			using var document = JsonDocument.Parse(json);
			Root = document.RootElement.Clone();
			if (Root.ValueKind != JsonValueKind.Object) throw Invalid("Expected a configuration object.");
			foreach (var property in new[] { "readinessAccess", "grNumber", "rollNumber", "divisionChange" })
				if (!Root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
					throw Invalid($"Missing {property} configuration.");
			if (!Root.TryGetProperty("onboardingProfiles", out var profiles) || profiles.ValueKind != JsonValueKind.Array)
				throw Invalid("Missing readiness rules.");
			foreach (var profile in Profiles)
			{
				if (profile.ValueKind != JsonValueKind.Object ||
					!profile.TryGetProperty("applicability", out var applicability) || applicability.ValueKind != JsonValueKind.Object ||
					!profile.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Object ||
					string.IsNullOrWhiteSpace(Text(profile, "id")) || Strings(profile, "admissionRoutes").Length == 0)
					throw Invalid("Each readiness rule needs an ID, admission route, applicability and rules.");
			}
			if (Profiles.Select(p => Text(p, "id")).Distinct().Count() != Profiles.Count())
				throw Invalid("Readiness rule IDs must be unique.");
			foreach (var section in new[] { "grNumber", "rollNumber", "divisionChange" })
				foreach (var rule in Rules(section))
				{
					if (rule.ValueKind != JsonValueKind.Object) throw Invalid($"Invalid {section} rule.");
					if (section == "grNumber") ValidateGrFormat(rule);
				}
		}
		catch (JsonException) { throw Invalid("Invalid configuration JSON."); }
	}

	private static void ValidateGrFormat(JsonElement rule)
	{
		var pattern = Text(rule, "formatPattern", "{PREFIX}{SEQUENCE}{SUFFIX}");
		if (pattern.Split("{SEQUENCE}").Length != 2)
			throw Invalid("GR format requires exactly one {SEQUENCE} token.");
		foreach (var token in new[] { "PREFIX", "SUFFIX", "YEAR", "YEAR_SHORT", "SEQUENCE" })
			pattern = pattern.Replace("{" + token + "}", "");
		if (pattern.Contains('{') || pattern.Contains('}') ||
			Text(rule, "prefix").IndexOfAny(['{', '}']) >= 0 ||
			Text(rule, "suffix").IndexOfAny(['{', '}']) >= 0)
			throw Invalid("GR format contains unsupported tokens or braces in its prefix/suffix.");
	}

	public IEnumerable<JsonElement> Profiles => Root.GetProperty("onboardingProfiles").EnumerateArray();
	public IEnumerable<JsonElement> Rules(string section) =>
		Root.GetProperty(section).TryGetProperty("configurations", out var rules) && rules.ValueKind == JsonValueKind.Array
			? rules.EnumerateArray() : throw Invalid($"Missing {section} rules.");
	public IEnumerable<JsonElement> Applicabilities => Profiles.Select(p => p.GetProperty("applicability"))
		.Concat(Rules("grNumber")).Concat(Rules("rollNumber")).Concat(Rules("divisionChange"));

	public JsonElement? ResolveProfile(StudentOnboardingCase item, string? route = null) =>
		Resolve(Profiles.Where(p => Strings(p, "admissionRoutes").Contains(route ?? item.GetAdmissionRoute())), item, true);
	public JsonElement Readiness(StudentOnboardingCase item) =>
		ResolveProfile(item)?.GetProperty("rules") ?? Root.GetProperty("readinessAccess");
	public JsonElement? ResolveRule(string section, StudentOnboardingCase item, DateTime at, Guid? divisionId = null) =>
		Resolve(Rules(section).Where(r => Boolean(r, "isActive", true) &&
			(!DateTime.TryParse(Text(r, section == "divisionChange" ? "effectiveDate" : "effectiveFrom"), out var from) || from.Date <= at.Date)), item, false, divisionId);

	private static JsonElement? Resolve(IEnumerable<JsonElement> rules, StudentOnboardingCase item,
		bool profile, Guid? divisionId = null)
	{
		var matches = rules.Where(r => Matches(profile ? r.GetProperty("applicability") : r, item, divisionId))
			.OrderByDescending(r => Score(profile ? r.GetProperty("applicability") : r)).ToList();
		if (matches.Count > 1 && Score(profile ? matches[0].GetProperty("applicability") : matches[0]) ==
			Score(profile ? matches[1].GetProperty("applicability") : matches[1]))
			throw Invalid("More than one rule has the same priority for this student. Narrow the rule coverage.");
		return matches.Count == 0 ? null : matches[0];
	}

	public static readonly (string Mode, string Ids, int Weight)[] Levels =
	[
		("boardMode", "boardIds", 16), ("mediumMode", "academicMediumIds", 8),
		("stageMode", "academicMediumStageIds", 4), ("classMode", "standardIds", 2),
		("divisionMode", "divisionIds", 1)
	];
	public static int Score(JsonElement rule) => Levels.Sum(l => Text(rule, l.Mode) == "selected" ? l.Weight : 0);
	public static bool Matches(JsonElement rule, StudentOnboardingCase item, Guid? divisionId = null)
	{
		Guid?[] path = [item.IntendedBoardId, item.IntendedMediumId, item.IntendedStageId,
			item.IntendedStandardId, divisionId ?? item.DivisionId];
		return Levels.Select((level, i) => Text(rule, level.Mode) == "all" ||
			path[i].HasValue && Ids(rule, level.Ids).Contains(path[i]!.Value)).All(value => value);
	}
	public static string Text(JsonElement value, string key, string fallback = "") =>
		value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString()! : fallback;
	public static bool Boolean(JsonElement value, string key, bool fallback = false) =>
		value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False ? property.GetBoolean() : fallback;
	public static long Number(JsonElement value, string key, long fallback = 0) =>
		value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number) ? number : fallback;
	public static string[] Strings(JsonElement value, string key) =>
		value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Array
			? property.EnumerateArray().Select(p => p.ValueKind == JsonValueKind.String ? p.GetString()! : throw Invalid($"Invalid {key}.")).ToArray() : [];
	public static Guid[] Ids(JsonElement value, string key) => Strings(value, key)
		.Select(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty ? parsed : throw Invalid($"Invalid {key}." )).ToArray();
	public static BusinessException Invalid(string reason) =>
		new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid).WithData("Reason", reason);
}
