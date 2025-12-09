using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments;
public class CreateUpdateInquiryDto : IValidatableObject
{
	[Required]
	public Guid AcademicMediumId { get; set; }
	[Required]
	public Guid StandardId { get; set; }
	[Required]
	public required string FirstName { get; set; }
	public string? MiddleName { get; set; }
	public required string LastName { get; set; }
	public DateTimeOffset DateOfBirth { get; set; }
	public GenderEnum Gender { get; set; }
	public string? PreviousSchool { get; set; }

	public DateTimeOffset DateOfInquiry { get; set; } = DateTimeOffset.Now;
	public Guid? InquiryPreferenceId { get; set; }

	public string? Email { get; set; }
	[Required]
	public required string PrimaryContactNo { get; set; }
	public string? SecondaryContactNo { get; set; }

	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public required string City { get; set; }
	public required string State { get; set; }
	public required string Country { get; set; }
	[RegularExpression(@"^\d{5,6}$", ErrorMessage = "Pin code must be 5-6 digits")]
	public string? PinCode { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// Custom validation logic
		if (DateOfBirth > DateTimeOffset.Now.AddYears(-3))
		{
			yield return new ValidationResult(
				"Date of birth must be at least 3 years ago",
				new[] { nameof(DateOfBirth) });
		}

		if (DateOfInquiry > DateTimeOffset.Now.AddDays(1))
		{
			yield return new ValidationResult(
				"Date of inquiry cannot be in the future",
				new[] { nameof(DateOfInquiry) });
		}
	}
}
