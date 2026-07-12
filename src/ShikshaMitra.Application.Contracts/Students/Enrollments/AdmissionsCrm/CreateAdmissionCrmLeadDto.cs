using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmLeadDto : IValidatableObject
{
	public Guid? InquirySourceId { get; set; }
	public AdmissionInquiryChannel Channel { get; set; } = AdmissionInquiryChannel.ManualBackOffice;
	public string? CampaignName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? CampusBranchName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? PreferredCommunicationLanguage { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? ParentGuardianName { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? Email { get; set; }
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxIntentLength)]
	public required string AdmissionIntent { get; set; }
	public Guid? TargetStandardId { get; set; }
	public string? DesiredClassName { get; set; }
	public string? Notes { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
	public string? DuplicateContinueReason { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (string.IsNullOrWhiteSpace(PrimaryContactNo) && string.IsNullOrWhiteSpace(Email))
		{
			yield return new ValidationResult(
				"Either primary contact number or email is required.",
				new[] { nameof(PrimaryContactNo), nameof(Email) });
		}
	}
}
