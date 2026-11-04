using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionFormVersionComparer
{
	public static List<string> Compare(AdmissionFormDto from, AdmissionFormDto to)
	{
		var changes = new List<string>();
		if (!string.Equals(from.Name, to.Name, StringComparison.Ordinal)) changes.Add($"Form name: '{from.Name}' to '{to.Name}'");
		if (!string.Equals(from.Description, to.Description, StringComparison.Ordinal)) changes.Add("Form description changed");
		if (from.Purpose != to.Purpose) changes.Add($"Purpose changed from {from.Purpose} to {to.Purpose}");
		if (from.LayoutType != to.LayoutType) changes.Add($"Layout changed from {from.LayoutType} to {to.LayoutType}");
		if (from.FieldsPerRow != to.FieldsPerRow) changes.Add($"Desktop columns changed from {from.FieldsPerRow} to {to.FieldsPerRow}");
		if (!string.Equals(from.DefaultLanguage, to.DefaultLanguage, StringComparison.Ordinal)) changes.Add("Default language changed");
		if (!from.SupportedLanguages.SequenceEqual(to.SupportedLanguages, StringComparer.OrdinalIgnoreCase)) changes.Add("Supported languages changed");
		if (from.CoreFieldPolicyVersion != to.CoreFieldPolicyVersion) changes.Add("Core field policy changed");
		if (!Equivalent(from.Inheritance, to.Inheritance)) changes.Add("Inquiry inheritance changed");
		var beforeSections = from.Sections.ToDictionary(section => section.Code, StringComparer.OrdinalIgnoreCase);
		var afterSections = to.Sections.ToDictionary(section => section.Code, StringComparer.OrdinalIgnoreCase);
		var beforeSteps = StepMemberships(from);
		var afterSteps = StepMemberships(to);
		foreach (var code in afterSections.Keys.Except(beforeSections.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Section added: {DisplayName(afterSections[code].Name, "Unnamed section")}");
		foreach (var code in beforeSections.Keys.Except(afterSections.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Section removed: {DisplayName(beforeSections[code].Name, "Unnamed section")}");
		foreach (var code in beforeSections.Keys.Intersect(afterSections.Keys, StringComparer.OrdinalIgnoreCase))
		{
			var before = beforeSections[code];
			var after = afterSections[code];
			var sectionName = DisplayName(after.Name, "Unnamed section");
			if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal)) changes.Add($"Section renamed: {NameChange(before.Name, after.Name, "Unnamed section")}");
			if (before.IsRepeatable != after.IsRepeatable || before.MinEntries != after.MinEntries || before.MaxEntries != after.MaxEntries)
				changes.Add($"Repeat settings changed: {sectionName}");
			if (!Equivalent(SectionBehavior(before, beforeSteps[code]), SectionBehavior(after, afterSteps[code]))) changes.Add($"Section changed: {sectionName}");
			var beforeFields = before.Fields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
			var afterFields = after.Fields.ToDictionary(field => field.Code, StringComparer.OrdinalIgnoreCase);
			foreach (var field in afterFields.Keys.Except(beforeFields.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Field added: {DisplayName(afterFields[field].Label, "Unnamed field")} ({sectionName})");
			foreach (var field in beforeFields.Keys.Except(afterFields.Keys, StringComparer.OrdinalIgnoreCase)) changes.Add($"Field removed: {DisplayName(beforeFields[field].Label, "Unnamed field")} ({DisplayName(before.Name, "Unnamed section")})");
			foreach (var field in beforeFields.Keys.Intersect(afterFields.Keys, StringComparer.OrdinalIgnoreCase))
				if (!Equivalent(FieldBehavior(beforeFields[field]), FieldBehavior(afterFields[field]))) changes.Add($"Field changed: {NameChange(beforeFields[field].Label, afterFields[field].Label, "Unnamed field")} ({sectionName})");
		}
		return changes;
	}
	private static string DisplayName(string? name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
	private static string NameChange(string? before, string? after, string fallback)
	{
		var beforeName = DisplayName(before, fallback);
		var afterName = DisplayName(after, fallback);
		return string.Equals(beforeName, afterName, StringComparison.Ordinal) ? afterName : $"{beforeName} → {afterName}";
	}

	// Record IDs, audit/status metadata and version numbers identify the snapshot;
	// they are not changes to the questions or their behavior.
	private static Dictionary<string, string[]> StepMemberships(AdmissionFormDto form)
	{
		var groups = form.Sections.GroupBy(section => section.StepId ?? section.Id)
			.ToDictionary(group => group.Key, group => group.Select(section => section.Code.ToUpperInvariant())
				.OrderBy(code => code, StringComparer.Ordinal).ToArray());
		return form.Sections.ToDictionary(section => section.Code, section => groups[section.StepId ?? section.Id], StringComparer.OrdinalIgnoreCase);
	}
	private static object SectionBehavior(AdmissionFormSectionDto section, string[] stepMembers) => new
	{
		section.DisplayOrder, section.FieldsPerRow, StepMembers = stepMembers, section.StepCode, section.StepName,
		section.StepDisplayOrder, section.EntryLabel, section.Translations,
		Visibility = Rule(section.VisibilityRuleJson), StepVisibility = Rule(section.StepVisibilityRuleJson)
	};
	private static object FieldBehavior(AdmissionFormFieldDto field) => new
	{
		field.Label, field.FieldType, field.BindingType, field.BindingKey, field.Required, field.ReadOnly,
		field.DisplayOrder, field.Placeholder, field.HelpText, field.DefaultValue, field.Translations,
		Options = Rule(field.OptionsJson), Validation = Rule(field.ValidationJson),
		Visibility = Rule(field.VisibilityRuleJson), RequiredRule = Rule(field.RequiredRuleJson)
	};
	private static bool Equivalent(object? before, object? after) =>
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(before), JsonSerializer.SerializeToNode(after));
	private static JsonNode? Rule(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		try { return JsonNode.Parse(json); }
		catch (JsonException) { return JsonValue.Create(json); }
	}
}
