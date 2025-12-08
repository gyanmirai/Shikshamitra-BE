using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// DTO for updating communication/address information of a student in Registration status
/// </summary>
public class UpdateCommunicationInfoDto
{
	/// <summary>
	/// Local/Current address where student currently resides
	/// </summary>
	public AddressDto? LocalAddress { get; set; }

	/// <summary>
	/// Permanent address as per official documents
	/// </summary>
	public AddressDto? PermanentAddress { get; set; }

	/// <summary>
	/// Emergency contact address
	/// </summary>
	public AddressDto? EmergencyAddress { get; set; }

	/// <summary>
	/// Indicates if permanent address is same as local address
	/// </summary>
	public bool IsPermanentSameAsLocal { get; set; }

	/// <summary>
	/// Indicates if emergency address is same as local address
	/// </summary>
	public bool IsEmergencySameAsLocal { get; set; }

	/// <summary>
	/// Emergency contact person name
	/// </summary>
	public string? EmergencyContactName { get; set; }

	/// <summary>
	/// Emergency contact person phone number
	/// </summary>
	public string? EmergencyContactPhone { get; set; }

	/// <summary>
	/// Relationship with emergency contact person
	/// </summary>
	public string? EmergencyContactRelation { get; set; }
}
