using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.AdditionalInfo;

/// <summary>
/// Request/Response DTO for employee additional information
/// </summary>
public class AdditionalInfoContract
{
	[MaxLength(50)]
	public string? BplCardNo { get; set; }

	public bool PersonWithDisability { get; set; }

	[MaxLength(100)]
	public string? SecondLanguage { get; set; }

	public bool IsLibraryMember { get; set; }

	public bool IsTransportMember { get; set; }
}
