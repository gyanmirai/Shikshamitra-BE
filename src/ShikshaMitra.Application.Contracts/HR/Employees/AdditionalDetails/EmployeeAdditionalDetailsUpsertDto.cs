namespace ShikshaMitra.HR.Employees.AdditionalDetails;

/// <summary>
/// DTO for upserting (create or update) employee additional details.
/// Used for PUT operations.
/// </summary>
public class EmployeeAdditionalDetailsUpsertDto
{
	/// <summary>
	/// Below Poverty Line card number, if applicable.
	/// </summary>
	public string? BplCardNo { get; set; }

	/// <summary>
	/// Indicates whether the employee is a person with disability.
	/// </summary>
	public bool PersonWithDisability { get; set; }

	/// <summary>
	/// Optional second language known by the employee.
	/// </summary>
	public string? SecondLanguage { get; set; }

	/// <summary>
	/// Whether the employee is enrolled as a library member.
	/// </summary>
	public bool LibraryMember { get; set; }

	/// <summary>
	/// Whether the employee is enrolled as a transport member.
	/// </summary>
	public bool TransportMember { get; set; }
}
