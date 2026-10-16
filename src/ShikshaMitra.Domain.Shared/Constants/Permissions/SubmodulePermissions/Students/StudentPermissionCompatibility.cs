using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Retired Student action names retain deny precedence after actions are bundled.
/// Positive legacy grants are deliberately not promoted to a broader capability.
/// </summary>
public static class StudentPermissionCompatibility
{
	private static readonly IReadOnlyDictionary<string, string[]> RetiredDenials =
		new Dictionary<string, string[]>(StringComparer.Ordinal)
		{
			["Students.Admissions.Workflows.Approve"] = [ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add],
			["Students.Admissions.Workflows.ManageGovernance"] = [ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add],
			["Students.Admissions.Workflows.Rollback"] = [ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add],
			["Students.Records.Settings.Publish"] = [ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add],
			["Students.Records.Changes.Create"] = [ShikshaMitraSubmodulePermissions.Students.Records_Changes.Add],
			["Students.Records.Changes.Approve"] = [ShikshaMitraSubmodulePermissions.Students.Records_Changes.Add],
			["Students.Records.Changes.Cancel"] = [ShikshaMitraSubmodulePermissions.Students.Records_Changes.Add, ShikshaMitraSubmodulePermissions.Students.Records_Changes.Delete],
			// Creating a transition previously also authorized its cancellation.
			["Students.Records.Transitions.Create"] =
				[ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Add, ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Delete],
			["Students.Records.Transitions.Approve"] = [ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Add],
			["Students.Records.Transitions.Apply"] = [ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Add],
			["Students.Records.Overrides.Request"] = [ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Add],
			["Students.Records.Overrides.Approve"] = [ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Add],
			["Students.Records.GuardianAccess.Manage"] = [ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.Add]
		};

	public static HashSet<string> IncludeCanonicalDenials(IEnumerable<string> names)
	{
		var result = names.ToHashSet(StringComparer.Ordinal);
		foreach (var name in result.ToArray())
			if (RetiredDenials.TryGetValue(name, out var canonicalNames)) result.UnionWith(canonicalNames);
		return result;
	}

	public static string[] GetRetiredDenialNames(string canonicalName) =>
		RetiredDenials.Where(item => item.Value.Contains(canonicalName, StringComparer.Ordinal)).Select(item => item.Key).ToArray();

	public static string[] GetCanonicalDenialNames(string retiredName) =>
		RetiredDenials.TryGetValue(retiredName, out var names) ? names.ToArray() : [];
}
