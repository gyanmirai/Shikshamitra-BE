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

	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	public string? MobileNo { get; set; }

	public DateOnly? DateOfJoining { get; set; }

	public Guid RoleId { get; set; }

	#endregion

	#region Optional Fields

	public string? MiddleName { get; set; }

	public string? ShortName { get; set; }

	public EmployeeStatusType EmployeeStatusType { get; set; } = EmployeeStatusType.Active;

	public DateOnly? DateOfBirth { get; set; }

	public string? FamilyCaste { get; set; }

	public CastType? CastType { get; set; }

	public GenderType? GenderType { get; set; }

	public string? Religion { get; set; }

	public MaritalStatusType? MaritalStatus { get; set; }

	public string? BloodGroup { get; set; }

	public string? SecondaryMobileNo { get; set; }

	public string? Email { get; set; }

	public string? Department { get; set; }

	public string? Designation { get; set; }

	public EmployeeGroupType? EmployeeGroup { get; set; }

	public string? Qualification { get; set; }

	public string? IdentificationMark1 { get; set; }

	public string? Address1 { get; set; }

	public string? Address2 { get; set; }

	public string? Address3 { get; set; }

	public Country? Country { get; set; }

	public State? State { get; set; }

	public string? City { get; set; }

	public string? Pincode { get; set; }

	public Guid? ShiftId { get; set; }

	public List<Guid> OrganizationIds { get; set; } = [];

	#endregion

	#region Validation

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
