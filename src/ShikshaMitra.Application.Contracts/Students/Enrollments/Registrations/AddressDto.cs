using ShikshaMitra.Common;

namespace ShikshaMitra.Students.Enrollments.Registrations;

/// <summary>
/// DTO representing address details
/// </summary>
public class AddressDto
{
	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public string? City { get; set; }
	public State? State { get; set; }
	public Country? Country { get; set; }
	public string? Pincode { get; set; }
}
