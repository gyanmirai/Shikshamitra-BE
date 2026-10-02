using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentRecords;

public static class StudentRecordsPolicyValidator
{
	public static StudentRecordsPolicy ParseAndValidate(string policyJson)
	{
		try
		{
			var policy = StudentRecordsPolicy.FromJson(policyJson);
			Validate(policy);
			return policy;
		}
		catch (JsonException)
		{
			throw new BusinessException(StudentRecordsErrorCodes.InvalidPolicy).WithData("Reason", "Invalid policy JSON.");
		}
	}

	public static void Validate(StudentRecordsPolicy policy)
	{
		Require(policy != null && policy.SchemaVersion == 1, "Unsupported policy version.");
		Require(policy!.Changes != null && policy.Provisioning != null && policy.Access != null && policy.GuardianAccess != null && policy.Documents != null && policy.Transitions != null, "All policy sections are required.");
		Require(policy.Changes!.Count == StudentRecordsPolicy.Actions.Length && StudentRecordsPolicy.Actions.All(policy.Changes.ContainsKey), "Configure every supported change action.");
		foreach (var pair in policy.Changes)
		{
			var rule = pair.Value;
			Require(rule != null, "Each change action requires a rule.");
			Choice(rule!.Mode, "Direct", "Approval");
			Require(rule.Stages != null && rule.Stages.Count <= 3, "Approval supports up to three ordered stages.");
			if (rule.Mode == "Approval") Require(rule.Stages!.Count >= 1, "Approval requires at least one stage.");
			foreach (var stage in rule.Stages!)
			{
				Require(stage != null, "Invalid approval stage.");
				Roles(stage!.RoleNames, false);
			}
			if (rule.Condition != null) Require(rule.Mode == "Approval" && (rule.Condition.IsPastDated.HasValue || rule.Condition.IsFutureDated.HasValue || rule.Condition.IsFormerStudent.HasValue), "Select at least one condition on an approval rule.");
			if (pair.Key == "LegacyOverride") Require(rule.Mode == "Approval" && rule.RequireReason && rule.Condition == null,
				"Historical directory overrides always require a reason and a different approver.");
		}
		Choice(policy.Provisioning!.Mode, "OnboardingOnly", "Summary", "Full");
		Choice(policy.GuardianAccess!.OnApprovedChange, "SuspendUntilReview", "KeepUntilReview");
		Require(!policy.Provisioning.AllowPlacementEdits || policy.Provisioning.Mode == "Full", "Placement editing requires full provisioning access.");
		Require(policy.Access!.Masks != null && policy.Access.Masks.Count <= StudentRecordsPolicy.SensitiveGroups.Length, "Invalid sensitive field masks.");
		var groups = new HashSet<string>(StringComparer.Ordinal);
		foreach (var mask in policy.Access.Masks!)
		{
			Require(mask != null && StudentRecordsPolicy.SensitiveGroups.Contains(mask.FieldGroup) && groups.Add(mask.FieldGroup), "Choose each supported sensitive group once.");
			Roles(mask!.AllowedRoleNames, true);
		}
		var documents = policy.Documents!;
		Require(documents.EnabledCertificateTypes != null && documents.EnabledCertificateTypes.Distinct().Count() == documents.EnabledCertificateTypes.Count &&
			documents.EnabledCertificateTypes.All(StudentRecordsPolicy.CertificateTypes.Contains), "Unsupported or duplicate certificate type.");
		Prefix(documents.CertificateNumberPrefix); Prefix(documents.IdNumberPrefix);
		Choice(documents.LeavingCertificateStage, "ApprovedExit", "EffectiveExit");
		var transition = policy.Transitions!;
		Require(transition.OutcomeChecks != null && transition.OutcomeChecks.Count == StudentRecordsPolicy.Outcomes.Length &&
			StudentRecordsPolicy.Outcomes.All(transition.OutcomeChecks.ContainsKey) && transition.OutcomeChecks.Values.All(x => x != null), "Configure checks for every transition outcome.");
		Require(transition.MaxPastDays is >= 0 and <= 3650 && transition.MaxFutureDays is >= 0 and <= 3650, "Date limits must be between zero and 3650 days.");
		Choice(transition.RosterRemovalTiming, "Approval", "EffectiveDate");
		Choice(transition.AnnualRenewalMode, "DirectEnrollment", "ShortChecklist");
		Choice(transition.BatchExceptionMode, "ReadyOnly", "HoldAll");
		Require(transition.AllowedReturnRoutes != null && transition.AllowedReturnRoutes.Count > 0 && transition.AllowedReturnRoutes.Distinct().Count() == transition.AllowedReturnRoutes.Count, "Choose distinct return routes.");
		foreach (var route in transition.AllowedReturnRoutes!) Choice(route, "ReuseProfile", "FreshApplication", "ResumeStage");
		Require(transition.AllowedReturnRoutes.Contains(transition.DefaultReturnRoute), "The default return route must be allowed.");
		Require(policy.ToJson().Length <= 100000, "Student Records policy exceeds the supported size.");
	}

	public static string[] RoleNames(StudentRecordsPolicy policy) => policy.Changes.Values.SelectMany(x => x.Stages).SelectMany(x => x.RoleNames)
		.Concat(policy.Access.Masks.SelectMany(x => x.AllowedRoleNames)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
	private static void Roles(List<string>? roles, bool allowEmpty) => Require(roles != null && (allowEmpty || roles.Count > 0) && roles.Count <= 50 &&
		roles.All(x => !string.IsNullOrWhiteSpace(x) && x == x.Trim() && x.Length <= 256) && roles.Distinct(StringComparer.OrdinalIgnoreCase).Count() == roles.Count, "Choose valid distinct central Security roles.");
	private static void Prefix(string prefix) => Require(!string.IsNullOrWhiteSpace(prefix) && prefix.Length <= 30 && prefix.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_'), "Number prefixes accept up to 30 letters, digits, hyphens, or underscores.");
	private static void Choice(string value, params string[] values) => Require(values.Contains(value, StringComparer.Ordinal), $"Unsupported policy choice: {value}.");
	private static void Require(bool condition, string reason)
	{
		if (!condition) throw new BusinessException(StudentRecordsErrorCodes.InvalidPolicy).WithData("Reason", reason);
	}
}
