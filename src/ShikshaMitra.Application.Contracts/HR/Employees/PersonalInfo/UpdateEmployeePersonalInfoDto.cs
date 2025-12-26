using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// DTO for updating an existing employee's personal information.
/// </summary>
public class UpdateEmployeePersonalInfoDto : IValidatableObject
{
	#region Required Fields for Update

	[MaxLength(256)]
	public string? FirstName { get; set; }

	[MaxLength(256)]
	public string? LastName { get; set; }

	[Phone]
	[MaxLength(15)]
	[RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Invalid mobile number. Must be 10 digits starting with 6-9.")]
	public string? MobileNo { get; set; }

	public DateOnly? DateOfJoining { get; set; }

	[Required(ErrorMessage = "Role is required.")]
	public Guid RoleId { get; set; }

	[Required(ErrorMessage = "Education Medium Stage is required.")]
	public Guid EducationMediumStageId { get; set; }

	#endregion

	#region Optional Fields

	[MaxLength(256)]
	public string? MiddleName { get; set; }

	[MaxLength(128)]
	public string? ShortName { get; set; }

	public EmployeeStatusType EmployeeStatusType { get; set; } = EmployeeStatusType.Active;

	public DateOnly? DateOfBirth { get; set; }

	[MaxLength(128)]
	public string? FamilyCaste { get; set; }

	public CastType? CastType { get; set; }

	public GenderType? GenderType { get; set; }

	[MaxLength(128)]
	public string? Religion { get; set; }

	public MaritalStatusType? MaritalStatus { get; set; }

	[MaxLength(10)]
	public string? BloodGroup { get; set; }

	[Phone]
	[MaxLength(15)]
	public string? SecondaryMobileNo { get; set; }

	[EmailAddress(ErrorMessage = "Invalid email address format.")]
	[MaxLength(256)]
	public string? Email { get; set; }

	[MaxLength(256)]
	public string? Department { get; set; }

	[MaxLength(256)]
	public string? Designation { get; set; }

	public EmployeeGroupType? EmployeeGroup { get; set; }

	[MaxLength(512)]
	public string? Qualification { get; set; }

	[MaxLength(256)]
	public string? IdentificationMark1 { get; set; }

	[MaxLength(512)]
	public string? Address1 { get; set; }

	[MaxLength(512)]
	public string? Address2 { get; set; }

	[MaxLength(512)]
	public string? Address3 { get; set; }

	public Country? Country { get; set; }

	public State? State { get; set; }

	[MaxLength(128)]
	public string? City { get; set; }

	[MaxLength(10)]
	[RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be 6 digits.")]
	public string? Pincode { get; set; }

	public Guid? ShiftId { get; set; }

	#endregion

	#region Validation

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		// Validate age >= 18 at joining date
		if (DateOfJoining.HasValue && DateOfBirth.HasValue)
		{
			var age = DateOfJoining.Value.Year - DateOfBirth.Value.Year;
			if (DateOfJoining.Value < DateOfBirth.Value.AddYears(age))
			{
				age--;
			}

			if (age < 18)
			{
				yield return new ValidationResult(
					"Employee must be at least 18 years old at the time of joining.",
					new[] { nameof(DateOfJoining), nameof(DateOfBirth) }
				);
			}
		}

		// Validate date of birth is not in the future
		if (DateOfBirth.HasValue && DateOfBirth.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
		{
			yield return new ValidationResult(
				"Date of birth cannot be in the future.",
				new[] { nameof(DateOfBirth) }
			);
		}

		// Validate date of joining is not before date of birth
		if (DateOfJoining.HasValue && DateOfBirth.HasValue && DateOfJoining.Value < DateOfBirth.Value)
		{
			yield return new ValidationResult(
				"Date of joining cannot be before date of birth.",
				new[] { nameof(DateOfJoining) }
			);
		}
	}

	#endregion
}

