using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmLeadDto : IValidatableObject
{
	public Guid? InquirySourceId { get; set; }
	public Guid? InquiryTypeId { get; set; }
	public AdmissionInquiryChannel Channel { get; set; } = AdmissionInquiryChannel.ManualBackOffice;
	[MaxLength(AdmissionCrmConsts.MaxCampaignLength)]
	public string? CampaignName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? CampusBranchName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? PreferredCommunicationLanguage { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? FirstName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? MiddleName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? LastName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? ParentGuardianName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxContactLength)]
	public string? PrimaryContactNo { get; set; }
	[EmailAddress]
	[MaxLength(AdmissionCrmConsts.MaxEmailLength)]
	public string? Email { get; set; }
	[Required]
	[MaxLength(AdmissionCrmConsts.MaxIntentLength)]
	public required string AdmissionIntent { get; set; }
	public AdmissionCrmRouteType? IntendedRouteType { get; set; }
	public Guid? IntendedRouteDefinitionId { get; set; }
	public Guid? TargetStandardId { get; set; }
	public Guid? AcademicBoardId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public Guid? AcademicMediumStageId { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNameLength)]
	public string? DesiredClassName { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxNotesLength)]
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
