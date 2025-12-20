using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.Enrollments.Admissions;

/// <summary>
/// Result DTO for batch admission status change
/// </summary>
public class BatchAdmissionStatusChangeResultDto
{
	public List<AdmissionStatusChangeItemResultDto> Results { get; set; } = new();
	public int Succeeded { get; set; }
	public int Failed { get; set; }
}

/// <summary>
/// Individual result for admission status change
/// </summary>
public class AdmissionStatusChangeItemResultDto
{
	public Guid StudentId { get; set; }
	public bool Success { get; set; }
	public string? Message { get; set; }
	public string? NewStatus { get; set; }
}
