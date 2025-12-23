using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.AdditionalDetails;

/// <summary>
/// DTO for partially updating employee additional details.
/// Used for PATCH operations. Only provided fields are updated.
/// </summary>
public class EmployeeAdditionalDetailsPatchDto
{
	/// <summary>
	/// Below Poverty Line card number.
	/// Set to null to clear, omit to leave unchanged.
	/// </summary>
	[MaxLength(50, ErrorMessage = "BPL Card No cannot exceed 50 characters.")]
	public string? BplCardNo { get; set; }

	/// <summary>
	/// Indicates whether BplCardNo was explicitly provided in the request.
	/// Used to differentiate between "not provided" and "set to null".
	/// </summary>
	public bool BplCardNoProvided { get; set; }

	/// <summary>
	/// Indicates whether the employee is a person with disability.
	/// </summary>
	public bool? PersonWithDisability { get; set; }

	/// <summary>
	/// Second language known by the employee.
	/// </summary>
	[MaxLength(50, ErrorMessage = "Second Language cannot exceed 50 characters.")]
	public string? SecondLanguage { get; set; }

	/// <summary>
	/// Whether the employee is enrolled as a library member.
	/// </summary>
	public bool? LibraryMember { get; set; }

	/// <summary>
	/// Whether the employee is enrolled as a transport member.
	/// </summary>
	public bool? TransportMember { get; set; }
}



