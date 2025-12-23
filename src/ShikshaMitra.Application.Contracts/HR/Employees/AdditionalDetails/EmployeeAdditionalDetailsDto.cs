using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Employees.AdditionalDetails;

/// <summary>
/// Response DTO for employee additional details.
/// </summary>
public class EmployeeAdditionalDetailsDto : EntityDto<Guid>
{
	/// <summary>
	/// The employee ID this additional details record belongs to.
	/// </summary>
	public Guid EmployeeId { get; set; }

	/// <summary>
	/// Below Poverty Line card number.
	/// </summary>
	public string? BplCardNo { get; set; }

	/// <summary>
	/// Indicates whether the employee is a person with disability.
	/// </summary>
	public bool PersonWithDisability { get; set; }

	/// <summary>
	/// Second language known by employee.
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

	/// <summary>
	/// UTC timestamp when the record was created.
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// UTC timestamp when the record was last modified.
	/// </summary>
	public DateTime LastModifiedAt { get; set; }
}



