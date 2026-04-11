using ShikshaMitra.Common;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees;

public class ContactDetailsDto
{
	[MaxLength(256)]
	[RegularExpression(@"^[a-zA-Z\s\.\-']+$", ErrorMessage = "Name contains invalid characters")]
	public string? Name { get; set; }

	[MaxLength(256)]
	[RegularExpression(@"^[a-zA-Z0-9\s\.\-,]+$", ErrorMessage = "Occupation contains invalid characters")]
	public string? Occupation { get; set; }

	[Phone]
	[MaxLength(20)]
	[RegularExpression(@"^\+?[0-9\s\-()]+$", ErrorMessage = "Invalid phone number format")]
	public string? ContactNo { get; set; }

	[MaxLength(512)]
	public string? Address1 { get; set; }

	[MaxLength(512)]
	public string? Address2 { get; set; }

	[MaxLength(512)]
	public string? Address3 { get; set; }

	[MaxLength(128)]
	[RegularExpression(@"^[a-zA-Z\s\.\-']+$", ErrorMessage = "City contains invalid characters")]
	public string? City { get; set; }

	[MaxLength(128)]
	public string? StateName { get; set; }

	[MaxLength(3)]
	[RegularExpression(@"^[A-Z]{2,3}$", ErrorMessage = "State ISO code must be 2-3 uppercase letters")]
	public string? StateIsoCode { get; set; }

	[MaxLength(128)]
	public string? CountryName { get; set; }

	[MaxLength(3)]
	[RegularExpression(@"^[A-Z]{2,3}$", ErrorMessage = "Country ISO code must be 2-3 uppercase letters")]
	public string? CountryIsoCode { get; set; }

	[MaxLength(10)]
	[RegularExpression(@"^[0-9]{6,10}$", ErrorMessage = "PIN code must be 6-10 digits")]
	public string? PinCode { get; set; }
}