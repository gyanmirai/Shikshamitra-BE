using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students;

/// <summary>A child's contact preference never creates or changes a portal association.</summary>
public static class StudentFamilyContactSelection
{
	public const string SelectionKey = "MainSchoolContactRelationship";
	public const string GuardianRelationshipKey = "OtherGuardianRelationship";

	public static string? Resolve(Student student, StudentFamilyDetail? family = null)
	{
		family ??= student.FamilyDetail;
		var saved = student.GetProperty<string>(SelectionKey);
		if (!string.IsNullOrEmpty(saved)) return Present(Profile(family, saved)) ? saved : null;
		var parents = new[] { "father", "mother" }.Where(role => Present(Profile(family, role))).ToArray();
		return parents.Length == 1 ? parents[0] : parents.Length == 0 && Present(family?.GuardianDetails) ? "guardian" : null;
	}

	public static void Select(Student student, string relationship, StudentFamilyDetail? family = null)
	{
		if (relationship is not ("father" or "mother" or "guardian") || !Present(Profile(family ?? student.FamilyDetail, relationship)))
			throw new BusinessException("Students:Family:MainContactInvalid", "Choose a saved parent or guardian as the main school contact.");
		student.SetProperty(SelectionKey, relationship);
	}

	public static ParentGuardianDetails? Profile(StudentFamilyDetail? family, string? relationship) => relationship switch
	{
		"father" => family?.FatherDetails,
		"mother" => family?.MotherDetails,
		"guardian" => family?.GuardianDetails,
		_ => null
	};

	public static bool Present(ParentGuardianDetails? profile) => profile != null &&
		(!string.IsNullOrWhiteSpace(profile.FullName) || !string.IsNullOrWhiteSpace(profile.Email) || !string.IsNullOrWhiteSpace(profile.ContactNo));

	public static IReadOnlyList<string> MessageRelationships(IReadOnlyCollection<string> available, string? selected)
	{
		if (!string.IsNullOrWhiteSpace(selected))
			return selected is "father" or "mother" or "guardian" && available.Contains(selected) ? [selected] : [];
		var parents = new[] { "father", "mother" }.Where(available.Contains).ToArray();
		return parents.Length > 0 ? parents : available.Contains("guardian") ? ["guardian"] : [];
	}

	/// <summary>Contact preference and relationship labels confer no account ownership.</summary>
	public static bool RequiresAccessReview(string beforeJson, string afterJson, IReadOnlyCollection<string>? linkedRelationships = null)
	{
		try
		{
			var before = JsonNode.Parse(beforeJson) as JsonObject;
			var after = JsonNode.Parse(afterJson) as JsonObject;
			if (before == null || after == null) return true;
			if (linkedRelationships is { Count: > 0 })
			{
				foreach (var relationship in linkedRelationships)
				{
					if (relationship is not ("father" or "mother" or "guardian")) return true;
					if (!JsonNode.DeepEquals(before[relationship + "Details"], after[relationship + "Details"])) return true;
				}
				return false;
			}
			foreach (var value in new[] { before, after })
			{
				value.Remove("mainContactRelationship");
				value.Remove("guardianRelationship");
				value.Remove("guardianResponsibilityConfirmed");
			}
			return !JsonNode.DeepEquals(before, after);
		}
		catch (JsonException) { return true; }
	}
}
