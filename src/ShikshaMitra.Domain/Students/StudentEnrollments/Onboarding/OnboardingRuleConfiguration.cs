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
			_ = AvailableSchoolLevelNames;
			ValidateReadinessRules(Root.GetProperty("readinessAccess"));
			foreach (var profile in Profiles)
			{
				if (profile.ValueKind != JsonValueKind.Object ||
					!profile.TryGetProperty("applicability", out var applicability) || applicability.ValueKind != JsonValueKind.Object ||
					!profile.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Object ||
					string.IsNullOrWhiteSpace(Text(profile, "id")) || Strings(profile, "admissionRoutes").Length == 0 ||
					Strings(profile, "admissionRoutes").Any(route => string.IsNullOrWhiteSpace(route) ||
						route.Length > 100 || route != route.Trim() || route.Any(char.IsControl)))
					throw Invalid("Each readiness rule needs an ID, admission route, applicability and rules.");
				ValidateReadinessRules(rules);
			}
			if (Profiles.Select(p => Text(p, "id")).Distinct().Count() != Profiles.Count())
				throw Invalid("Readiness rule IDs must be unique.");
			ValidateChoice(Root.GetProperty("grNumber"), "assignmentMode", "both", "automatic", "manual");
			ValidateChoice(Root.GetProperty("rollNumber"), "assignmentMode", "both", "automatic", "manual");
			foreach (var section in new[] { "grNumber", "rollNumber", "divisionChange" })
				foreach (var rule in Rules(section))
				{
					if (rule.ValueKind != JsonValueKind.Object) throw Invalid($"Invalid {section} rule.");
					ValidateBoolean(rule, "isActive");
					ValidateInteger(rule, "maximumNumber", true);
					if (section == "grNumber")
					{
						ValidateGrFormat(rule);
						ValidateChoice(rule, "sequenceScope", "school", "academic-year");
						ValidateChoice(rule, "resetPolicy", "continuous", "academic-year");
						ValidateBoolean(rule, "manualEntryAllowed");
						ValidateBoolean(rule, "requireManualApproval");
					}
					if (section == "rollNumber")
					{
						ValidateBoolean(rule, "requireChangeReason");
						ValidateBoolean(rule, "requireApproval");
						ValidateChoice(rule, "numberingScope", "class-division", "class");
					}
					if (section == "divisionChange")
					{
						ValidateBoolean(rule, "requireReason");
						ValidateBoolean(rule, "requireApproval");
					}
				}
		}
		catch (JsonException) { throw Invalid("Invalid configuration JSON."); }
	}

	private static void ValidateChoice(JsonElement value, string key, params string[] choices)
	{
		if (!value.TryGetProperty(key, out var property)) return;
		if (property.ValueKind != JsonValueKind.String ||
			!choices.Contains(property.GetString(), StringComparer.Ordinal))
			throw Invalid($"Unsupported {key} in onboarding settings.");
	}

	private static void ValidateReadinessRules(JsonElement rules)
	{
		foreach (var key in new[] { "requireDocuments", "studentAccount", "guardianAccount", "sendStudentCredentialEmail", "sendGuardianCredentialEmail" })
			ValidateBoolean(rules, key);
		ValidateChoice(rules, "credentialSendingTime", "activation", "completion");
		ValidateChoice(rules, "credentialProvisioningMode", "manual", "automatic");
		ValidateChoice(rules, "guardianAccountRelationship", "father", "mother", "guardian");
		if (Boolean(rules, "sendStudentCredentialEmail") && !Boolean(rules, "studentAccount") ||
			Boolean(rules, "sendGuardianCredentialEmail") && !Boolean(rules, "guardianAccount"))
			throw Invalid("Enable the corresponding portal account before enabling its credential email.");
		ValidateChoice(rules, "approvalMode", "none", "always", "conditional");
		if (rules.TryGetProperty("approvalTriggers", out var triggers))
		{
			if (triggers.ValueKind != JsonValueKind.Array)
				throw Invalid("Invalid approval triggers in onboarding readiness settings.");
			_ = Strings(rules, "approvalTriggers");
		}
		if (rules.TryGetProperty("checks", out var checks))
		{
			if (checks.ValueKind != JsonValueKind.Array)
				throw Invalid("Invalid readiness checks in onboarding settings.");
			foreach (var check in checks.EnumerateArray())
			{
				if (check.ValueKind != JsonValueKind.Object)
					throw Invalid("Invalid readiness check in onboarding settings.");
				ValidateBoolean(check, "required");
			}
		}
	}

	private static void ValidateBoolean(JsonElement value, string key)
	{
		if (value.TryGetProperty(key, out var property) &&
			property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
			throw Invalid($"Invalid {key} in onboarding settings.");
	}

	private static void ValidateInteger(JsonElement value, string key, bool allowNull = false)
	{
		if (value.TryGetProperty(key, out var property) &&
			!(allowNull && property.ValueKind == JsonValueKind.Null) &&
			(property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out _)))
			throw Invalid($"Invalid {key} in onboarding settings.");
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
	public static string? FindBuiltInAdmissionRoute(string? requested) =>
		new[] { "Fresh admission", "Transfer student", "Mid-year admission", "Returning student" }
			.FirstOrDefault(route => string.Equals(route, requested?.Trim(), StringComparison.OrdinalIgnoreCase));
	public string? FindAdmissionRoute(string? requested) => Profiles
		.SelectMany(profile => Strings(profile, "admissionRoutes"))
		.FirstOrDefault(route => string.Equals(route, requested?.Trim(), StringComparison.OrdinalIgnoreCase));
	public static void EnsureAdmissionRouteCovered(StudentOnboardingCase item, OnboardingRuleConfiguration? configuration)
	{
		if (FindBuiltInAdmissionRoute(item.GetAdmissionRoute()) == null &&
			(configuration == null || !configuration.ResolveProfile(item).HasValue))
			throw Invalid("No readiness rule covers this school's admission route and academic path. Choose a covered route or restore its rule.");
	}
	public string[]? AvailableSchoolLevelNames
	{
		get
		{
			if (!Root.TryGetProperty("academicAvailability", out var availability)) return null;
			if (availability.ValueKind != JsonValueKind.Object ||
				!availability.TryGetProperty("schoolLevelNames", out var names))
				throw Invalid("Invalid onboarding school-level availability.");
			if (names.ValueKind == JsonValueKind.Null) return null;
			if (names.ValueKind != JsonValueKind.Array) throw Invalid("Invalid onboarding school-level availability.");
			var values = Strings(availability, "schoolLevelNames");
			if (values.Length == 0 || values.Length > 32 ||
				values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 100 || value != value.Trim()) ||
				values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
				throw Invalid("Choose distinct, valid onboarding school levels.");
			return values;
		}
	}
	public IEnumerable<JsonElement> Rules(string section) =>
		Root.GetProperty(section).TryGetProperty("configurations", out var rules) && rules.ValueKind == JsonValueKind.Array
			? rules.EnumerateArray() : throw Invalid($"Missing {section} rules.");
	public IEnumerable<JsonElement> Applicabilities => Profiles.Select(p => p.GetProperty("applicability"))
		.Concat(Rules("grNumber")).Concat(Rules("rollNumber")).Concat(Rules("divisionChange"));

	public JsonElement? ResolveProfile(StudentOnboardingCase item, string? route = null) =>
		Resolve(Profiles.Where(p => Strings(p, "admissionRoutes").Any(value =>
			string.Equals(value, route ?? item.GetAdmissionRoute(), StringComparison.OrdinalIgnoreCase))), item, true);
	public JsonElement Readiness(StudentOnboardingCase item) =>
		ResolveProfile(item)?.GetProperty("rules") ?? Root.GetProperty("readinessAccess");
	public JsonElement? ResolveRule(string section, StudentOnboardingCase item, DateTime at, Guid? divisionId = null) =>
		Resolve(Rules(section).Where(r => Boolean(r, "isActive", true) &&
			(!DateTime.TryParse(Text(r, section == "divisionChange" ? "effectiveDate" : "effectiveFrom"), out var from) || from.Date <= at.Date)), item, false, divisionId);

	/// <summary>Resolves the same owner rule for a real academic path without manufacturing an Admission case.</summary>
	public JsonElement? ResolveRule(string section, Guid boardId, Guid mediumId, Guid stageId, Guid standardId,
		Guid? divisionId, Guid? campusId, DateTime at)
	{
		Guid?[] path = [boardId, mediumId, stageId, standardId, divisionId];
		var matches = Rules(section).Where(rule => Boolean(rule, "isActive", true) &&
			(!DateTime.TryParse(Text(rule, section == "divisionChange" ? "effectiveDate" : "effectiveFrom"), out var from) || from.Date <= at.Date) &&
			(Text(rule, "campusMode", "all") != "selected" || campusId.HasValue && Ids(rule, "campusIds").Contains(campusId.Value)) &&
			Levels.Select((level, i) => Text(rule, level.Mode) == "all" || path[i].HasValue && Ids(rule, level.Ids).Contains(path[i]!.Value)).All(x => x))
			.OrderByDescending(Score).ToList();
		if (matches.Count > 1 && Score(matches[0]) == Score(matches[1])) throw Invalid("More than one rule has the same priority for this academic path. Narrow the rule coverage.");
		return matches.Count == 0 ? null : matches[0];
	}

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
	public static int Score(JsonElement rule) =>
		(Text(rule, "campusMode", "all") == "selected" ? 32 : 0) +
		Levels.Sum(l => Text(rule, l.Mode) == "selected" ? l.Weight : 0);
	public static bool Matches(JsonElement rule, StudentOnboardingCase item, Guid? divisionId = null)
	{
		if (Text(rule, "campusMode", "all") == "selected" &&
			(!item.GetCampusId().HasValue || !Ids(rule, "campusIds").Contains(item.GetCampusId()!.Value)))
			return false;
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
