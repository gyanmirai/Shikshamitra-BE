using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadDuplicatePreviewInput : IValidatableObject
{
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? ParentGuardianName { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? Email { get; set; }
	public string? AdmissionIntent { get; set; }
	public Guid? TargetStandardId { get; set; }
	public string? DesiredClassName { get; set; }

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
