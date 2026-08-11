using ShikshaMitra.Common;
using System;

namespace ShikshaMitra.Students.Records.Profiles;

/// <summary>
/// Shared general information fields used by registration create and update requests.
/// </summary>
public abstract class RegistrationGeneralInfoBaseDto
{
	public required string FormNumber { get; set; }
	public required string FirstName { get; set; }
	public string? MiddleName { get; set; }
	public required string LastName { get; set; }
	public DateTimeOffset DateOfBirth { get; set; }
	public GenderEnum Gender { get; set; }
	public string? ProfilePictureUrl { get; set; }

	public required string PrimaryContactNo { get; set; }
	public string? SecondaryContactNo { get; set; }
	public string? Email { get; set; }

	public string? AadharNo { get; set; }
	public string? ApparId { get; set; }
	public string? PanNo { get; set; }
	public string? PennNo { get; set; }
	public ReligionEnum? Religion { get; set; }
	public required string FamilyCast { get; set; }
	public CastCategoryEnum CastCategory { get; set; }
	public string? Nationality { get; set; }
	public string? BirthPlace { get; set; }
	public MotherTongueEnum? MotherTongue { get; set; }
	public Guid? InquirySourceId { get; set; }

	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public required string City { get; set; }
	public required State State { get; set; }
	public required Country Country { get; set; }
	public string? Pincode { get; set; }

	public string? PreviousSchool { get; set; }
	public string? LastClassPassed { get; set; }
}
