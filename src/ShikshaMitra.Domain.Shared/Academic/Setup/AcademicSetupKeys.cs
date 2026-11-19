using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Setup;

public static class AcademicSetupKeys
{
	public const string Invalid = "ShikshaMitra:AcademicSetup:Invalid";
	public const string InvalidStoredState = "ShikshaMitra:AcademicSetup:InvalidStoredState";
	public static readonly IReadOnlyDictionary<string, string> SettingStages = new Dictionary<string, string>(StringComparer.Ordinal) {
		["subjects"] = "curriculum", ["elective-groups"] = "curriculum",
		["weekly-lectures"] = "timetable", ["lecture-slots"] = "timetable",
		["homework-types"] = "homework", ["assignment-types"] = "homework", ["rubrics"] = "homework",
		["grading-scales"] = "examination", ["exam-types"] = "examination"
	};
	public static bool IsStage(string? key) => key is "welcome" or "curriculum" or "timetable" or "homework" or "examination" or "review";
}
