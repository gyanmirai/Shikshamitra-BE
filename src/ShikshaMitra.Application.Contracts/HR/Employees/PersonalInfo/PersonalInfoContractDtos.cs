using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

/// <summary>
/// Request DTO for creating or updating employee personal information
/// </summary>
public class PersonalInfoRequest : IValidatableObject
{
	[Required]
	[MaxLength(256)]
	public string FirstName { get; set; } = string.Empty;

	[MaxLength(256)]
	public string? MiddleName { get; set; }

	[Required]
	[MaxLength(256)]
	public string LastName { get; set; } = string.Empty;

	[MaxLength(128)]
	public string? ShortName { get; set; }

	public DateTime? Dob { get; set; }

	/// <summary>
	/// Gender: 1 = Male, 2 = Female
	/// </summary>
	public int? Gender { get; set; }

	[MaxLength(128)]
	public string? Religion { get; set; }

	/// <summary>
	/// Caste Type: 0 = General, 1 = OBC, 2 = SC, 3 = ST
	/// </summary>
	public int? CasteType { get; set; }

	[MaxLength(128)]
	public string? FamilyCaste { get; set; }

	/// <summary>
	/// Marital Status: 0 = Unmarried, 1 = Married, 2 = Divorced, 3 = Widowed
	/// </summary>
	public int? MaritalStatus { get; set; }

	/// <summary>
	/// Blood Group: 1 = A+, 2 = A-, 3 = B+, 4 = B-, 5 = AB+, 6 = AB-, 7 = O+, 8 = O-
	/// </summary>
	public int? BloodGroup { get; set; }

	[EmailAddress]
	[MaxLength(256)]
	public string? Email { get; set; }

	[Required]
	[RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Invalid mobile number. Must be 10 digits starting with 6-9.")]
	[MaxLength(15)]
	public string MobileNo { get; set; } = string.Empty;

	[MaxLength(15)]
	public string? SecondaryMobileNo { get; set; }

	[Required]
	public DateTime Doj { get; set; }

	[Required]
	public bool IsTeaching { get; set; }

	public string? Department { get; set; }

	[Required]
	public Guid RoleId { get; set; }

	[Required]
	public Guid ShiftId { get; set; }

	[MaxLength(512)]
	public string? Address1 { get; set; }

	[MaxLength(512)]
	public string? Address2 { get; set; }

	[MaxLength(512)]
	public string? Address3 { get; set; }

	public CountryInfo? Country { get; set; }

	public StateInfo? State { get; set; }

	[MaxLength(128)]
	public string? City { get; set; }

	[MaxLength(10)]
	[RegularExpression(@"^$|^\d{6}$", ErrorMessage = "Pincode must be 6 digits.")]
	public string? PinCode { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (!string.IsNullOrWhiteSpace(SecondaryMobileNo) && !string.IsNullOrWhiteSpace(MobileNo))
		{
			var primaryDigits = new string(MobileNo.Where(char.IsDigit).ToArray());
			var secondaryDigits = new string(SecondaryMobileNo.Where(char.IsDigit).ToArray());
			if (primaryDigits.Length > 0 && secondaryDigits.Length > 0 && primaryDigits == secondaryDigits)
			{
				yield return new ValidationResult(
					"Secondary mobile number must be different from the primary mobile number.",
					[nameof(SecondaryMobileNo)]);
			}
		}
	}
}

/// <summary>
/// Response DTO for employee personal information
/// </summary>
public class PersonalInfoResponse : PersonalInfoRequest
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string EmployeeCode { get; set; } = string.Empty;

	[Required]
	public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// Country information
/// </summary>
public class CountryInfo
{
	[Required]
	public string Name { get; set; } = string.Empty;

	[Required]
	public string IsoCode { get; set; } = string.Empty;
}

/// <summary>
/// State information
/// </summary>
public class StateInfo
{
	[Required]
	public string Name { get; set; } = string.Empty;

	[Required]
	public string IsoCode { get; set; } = string.Empty;
}
