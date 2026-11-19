using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentRecords;

/// <summary>Versioned school policy shared by configuration and record operations.</summary>
public sealed class StudentRecordsPolicy
{
	public int SchemaVersion { get; set; } = 1;
	public Dictionary<string, StudentRecordsChangeRule> Changes { get; set; } = Actions.ToDictionary(x => x,
		x => new StudentRecordsChangeRule { Mode = x is "Contact" or "Additional" or "Photo" or "PreviousSchool" ? "Direct" : "Approval" });
	public StudentRecordsProvisioningPolicy Provisioning { get; set; } = new();
	public StudentRecordsAccessPolicy Access { get; set; } = new();
	public StudentRecordsGuardianAccessPolicy GuardianAccess { get; set; } = new();
	public StudentRecordsDocumentPolicy Documents { get; set; } = new();
	public StudentRecordsTransitionPolicy Transitions { get; set; } = new();
	public static readonly string[] Actions = ["Identity", "Contact", "Guardian", "Medical", "Bank", "Additional", "PreviousSchool", "Placement", "Status", "Correction", "LegacyOverride", "Photo"];
	public static readonly string[] Outcomes = ["Promote", "Repeat", "Hold", "Graduate", "Withdraw", "Transfer", "Return"];
	public static readonly string[] SensitiveGroups = ["Identity", "Contact", "Medical", "Bank", "Guardian"];
	public static readonly string[] CertificateTypes = ["Bonafide", "Character", "Leaving", "FirstTrial"];
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	public static StudentRecordsPolicy Starter() => new();
	public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
	public static StudentRecordsPolicy FromJson(string json) => JsonSerializer.Deserialize<StudentRecordsPolicy>(json, JsonOptions)
		?? throw new JsonException("Student Records policy must be an object.");
	public bool IsMasked(string fieldGroup, IEnumerable<string> currentRoleNames) => Access.Masks.Any(mask =>
		mask.FieldGroup == fieldGroup && !mask.AllowedRoleNames.Intersect(currentRoleNames, StringComparer.OrdinalIgnoreCase).Any());
}

public sealed class StudentRecordsChangeRule
{
	public string Mode { get; set; } = "Approval";
	public bool RequireReason { get; set; } = true;
	public List<StudentRecordsApprovalStage> Stages { get; set; } = [new()];
	public StudentRecordsApprovalCondition? Condition { get; set; }
	/// <summary>An approval condition narrows the approval rule; unmatched requests use audited direct execution.</summary>
	public bool RequiresApproval(bool isPastDated, bool isFutureDated, bool isFormerStudent) => Mode == "Approval" &&
		(Condition == null || ((!Condition.IsPastDated.HasValue || Condition.IsPastDated == isPastDated) &&
			(!Condition.IsFutureDated.HasValue || Condition.IsFutureDated == isFutureDated) &&
			(!Condition.IsFormerStudent.HasValue || Condition.IsFormerStudent == isFormerStudent)));
}

public sealed class StudentRecordsApprovalStage
{
	public List<string> RoleNames { get; set; } = ["admin"];
}

/// <summary>Only supported, deterministic request attributes may condition approval.</summary>
public sealed class StudentRecordsApprovalCondition
{
	public bool? IsPastDated { get; set; }
	public bool? IsFutureDated { get; set; }
	public bool? IsFormerStudent { get; set; }
}

public sealed class StudentRecordsProvisioningPolicy
{
	public string Mode { get; set; } = "Summary";
	public bool AllowPlacementEdits { get; set; }
}

public sealed class StudentRecordsAccessPolicy
{
	public List<StudentRecordsFieldMask> Masks { get; set; } = [];
}

public sealed class StudentRecordsGuardianAccessPolicy
{
	public string OnApprovedChange { get; set; } = "SuspendUntilReview";
}

public sealed class StudentRecordsFieldMask
{
	public string FieldGroup { get; set; } = "Medical";
	public List<string> AllowedRoleNames { get; set; } = [];
}

public sealed class StudentRecordsDocumentPolicy
{
	public List<string> EnabledCertificateTypes { get; set; } = ["Bonafide", "Character", "Leaving", "FirstTrial"];
	public string CertificateNumberPrefix { get; set; } = "CERT";
	public string IdNumberPrefix { get; set; } = "ID";
	public string LeavingCertificateStage { get; set; } = "ApprovedExit";
}

public sealed class StudentRecordsTransitionPolicy
{
	public Dictionary<string, StudentRecordsOutcomeChecks> OutcomeChecks { get; set; } = StudentRecordsPolicy.Outcomes.ToDictionary(x => x, _ => new StudentRecordsOutcomeChecks());
	public bool AllowPastDates { get; set; }
	public int MaxPastDays { get; set; } = 30;
	public bool AllowFutureDates { get; set; } = true;
	public int MaxFutureDays { get; set; } = 365;
	public string RosterRemovalTiming { get; set; } = "EffectiveDate";
	public List<string> AllowedReturnRoutes { get; set; } = ["ReuseProfile", "FreshApplication"];
	public string DefaultReturnRoute { get; set; } = "ReuseProfile";
	public string AnnualRenewalMode { get; set; } = "ShortChecklist";
	public string BatchExceptionMode { get; set; } = "ReadyOnly";
}

public sealed class StudentRecordsOutcomeChecks
{
	public bool RequireAcademicResult { get; set; }
	public bool RequireFinanceClearance { get; set; }
	public bool RequireLibraryClearance { get; set; }
	public bool RequireTransportClearance { get; set; }
}
