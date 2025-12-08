using System;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// DTO representing address details
/// </summary>
public class AddressDto
{
	public string? BuildingNo { get; set; }
	public string? AreaLocalityRoadName { get; set; }
	public string? Landmark { get; set; }
	public string? State { get; set; }
	public string? City { get; set; }
	public string? Pincode { get; set; }
	public string? Country { get; set; }
}
