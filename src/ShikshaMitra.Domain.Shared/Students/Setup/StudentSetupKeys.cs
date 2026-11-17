using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Setup;

public static class StudentSetupKeys
{
	public const string Invalid = "ShikshaMitra:StudentSetup:Invalid";
	public const string InvalidStoredState = "ShikshaMitra:StudentSetup:InvalidStoredState";
	public static readonly IReadOnlyDictionary<string, string> SettingStages = new Dictionary<string, string>(StringComparer.Ordinal) {
		["admission-periods"] = "admissions", ["admission-forms"] = "admissions", ["admission-stages"] = "admissions",
		["admission-offerings"] = "admissions", ["public-inquiry-links"] = "admissions",
		["gr-number"] = "numbering", ["roll-number"] = "numbering",
		["houses"] = "records", ["records-policy"] = "records"
	};
	public static bool IsStage(string? key) => key is "welcome" or "admissions" or "numbering" or "records" or "workflows" or "review";
}
