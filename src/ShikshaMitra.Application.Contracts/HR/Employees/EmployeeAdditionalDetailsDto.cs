using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// DTO for employee additional details (response)
/// </summary>
public class EmployeeAdditionalDetailsDto
{
	/// <summary>
	/// Employee code for reference
	/// </summary>
	public string EmployeeCode { get; set; }

	/// <summary>
	/// Below Poverty Line card number
	/// </summary>
	[MaxLength(50)]
	public string? BplCardNo { get; set; }

	/// <summary>
	/// Person with disability status
	/// Allowed values: "Yes", "No", or null
	/// </summary>
	[MaxLength(3)]
	public string? PersonWithDisability { get; set; }

	/// <summary>
	/// Second language known
	/// </summary>
	[MaxLength(50)]
	public string? SecondLanguage { get; set; }

	/// <summary>
	/// Library membership status
	/// </summary>
	public bool LibraryMember { get; set; }

	/// <summary>
	/// Transport facility membership status
	/// </summary>
	public bool TransportMember { get; set; }
}

/// <summary>
/// DTO for updating employee additional details
/// </summary>
public class UpdateEmployeeAdditionalDetailsDto
{
	/// <summary>
	/// Below Poverty Line card number
	/// </summary>
	[MaxLength(50, ErrorMessage = "BPL Card No cannot exceed 50 characters")]
	public string? BplCardNo { get; set; }

	/// <summary>
	/// Person with disability status
	/// Must be "Yes", "No", or null
	/// </summary>
	[MaxLength(3)]
	[RegularExpression("^(Yes|No)$", ErrorMessage = "PersonWithDisability must be either 'Yes' or 'No'")]
	public string? PersonWithDisability { get; set; }

	/// <summary>
	/// Second language known
	/// </summary>
	[MaxLength(50, ErrorMessage = "Second Language cannot exceed 50 characters")]
	public string? SecondLanguage { get; set; }

	/// <summary>
	/// Library membership status
	/// Default: false
	/// </summary>
	public bool LibraryMember { get; set; } = false;

	/// <summary>
	/// Transport facility membership status
	/// Default: false
	/// </summary>
	public bool TransportMember { get; set; } = false;
}
