using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// DTO for employee additional details (response).
/// Used by the legacy API at /api/app/employee/{employeeId}/additional-details
/// </summary>
public class EmployeeAdditionalDetailsDto
{
	/// <summary>
	/// Employee code for reference.
	/// </summary>
	public string EmployeeCode { get; set; }

	/// <summary>
	/// Below Poverty Line card number.
	/// </summary>
	[MaxLength(50)]
	public string? BplCardNo { get; set; }

	/// <summary>
	/// Indicates if the employee is a person with disability.
	/// </summary>
	public bool PersonWithDisability { get; set; }

	/// <summary>
	/// Second language known.
	/// </summary>
	[MaxLength(50)]
	public string? SecondLanguage { get; set; }

	/// <summary>
	/// Library membership status.
	/// </summary>
	public bool LibraryMember { get; set; }

	/// <summary>
	/// Transport facility membership status.
	/// </summary>
	public bool TransportMember { get; set; }
}

/// <summary>
/// DTO for updating employee additional details.
/// Used by the legacy API at /api/app/employee/{employeeId}/additional-details
/// </summary>
public class UpdateEmployeeAdditionalDetailsDto
{
	/// <summary>
	/// Below Poverty Line card number.
	/// </summary>
	[MaxLength(50, ErrorMessage = "BPL Card No cannot exceed 50 characters")]
	public string? BplCardNo { get; set; }

	/// <summary>
	/// Indicates if the employee is a person with disability.
	/// </summary>
	public bool PersonWithDisability { get; set; }

	/// <summary>
	/// Second language known.
	/// </summary>
	[MaxLength(50, ErrorMessage = "Second Language cannot exceed 50 characters")]
	public string? SecondLanguage { get; set; }

	/// <summary>
	/// Library membership status.
	/// Default: false
	/// </summary>
	public bool LibraryMember { get; set; } = false;

	/// <summary>
	/// Transport facility membership status.
	/// Default: false
	/// </summary>
	public bool TransportMember { get; set; } = false;
}
