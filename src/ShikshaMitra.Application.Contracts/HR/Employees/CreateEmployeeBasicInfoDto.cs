using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees;

public class CreateEmployeeBasicInfoDto : IValidatableObject
{
	[MaxLength(256)]
	public string? LastName { get; set; }

	[Required(ErrorMessage = "First name is required.")]
	[MaxLength(256)]
	public string FirstName { get; set; }

	[MaxLength(256)]
	public string? MiddleName { get; set; }

	[MaxLength(128)]
	public string? ShortName { get; set; }

	public EmployeeStatusType EmployeeStatusType { get; set; }

	public DateOnly? DateOfBirth { get; set; }

	[MaxLength(128)]
	public string? FamilyCast { get; set; }

	public CastType? CastType { get; set; }
	public GenderType? GenderType { get; set; }

	[MaxLength(128)]
	public string? Religion { get; set; }

	public MaritalStatusType? MaritalStatus { get; set; }

	[MaxLength(20)]
	public string? BloodGroup { get; set; }

	[Phone]
	[MaxLength(20)]
	public string? MobileNo { get; set; }

	[Phone]
	[MaxLength(20)]
	public string? SecondaryMobileNo { get; set; }

	[EmailAddress]
	[MaxLength(256)]
	public string? Email { get; set; }

	[MaxLength(256)]
	public string? Department { get; set; }

	[MaxLength(256)]
	public string? Designation { get; set; }

	public DateOnly? DateOfJoining { get; set; }

	public EmployeeGroupType? EmployeeGroup { get; set; }

	[MaxLength(512)]
	public string? Qualification { get; set; }

	[Required(ErrorMessage = "Role is required.")]
	public Guid RoleId { get; set; }

	[MaxLength(256)]
	public string? IdentificationMark1 { get; set; }

	[MaxLength(128)]
	public string? City { get; set; }

	public State? State { get; set; }
	public Country? Country { get; set; }

	[MaxLength(20)]
	public string? PinCode { get; set; }

	[MaxLength(512)]
	public string? Address1 { get; set; }

	[MaxLength(512)]
	public string? Address2 { get; set; }

	[MaxLength(512)]
	public string? Address3 { get; set; }

	public Guid? ShiftId { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
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

		if (DateOfBirth.HasValue && DateOfBirth.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
		{
			yield return new ValidationResult(
				"Date of birth cannot be in the future.",
				new[] { nameof(DateOfBirth) }
			);
		}
	}
}