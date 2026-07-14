using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class UpdateAdmissionCrmEligibilityDecisionDto
{
	public AdmissionCrmEligibilityDecision Decision { get; set; }

	[Required]
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public required string Reason { get; set; }

	public int? MeritRank { get; set; }
	public bool OverrideDecision { get; set; }

	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? OverrideReason { get; set; }
}
