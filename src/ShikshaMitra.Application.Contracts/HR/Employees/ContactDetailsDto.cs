using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees;

public class ContactDetailsDto
{
	public string? Name { get; set; }
	public string? Occupation { get; set; }
	public string? ContactNo { get; set; }
	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public string? City { get; set; }
	public string? StateName { get; set; }
	public string? StateIsoCode { get; set; }
	public string? CountryName { get; set; }
	public string? CountryIsoCode { get; set; }
	public string? PinCode { get; set; }
}