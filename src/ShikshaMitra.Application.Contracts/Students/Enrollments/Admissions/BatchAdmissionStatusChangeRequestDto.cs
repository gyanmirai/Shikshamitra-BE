using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Admissions;

/// <summary>
/// Request DTO for batch admission status change
/// </summary>
public class BatchAdmissionStatusChangeRequestDto
{
	[Required]
	[MinLength(1, ErrorMessage = "At least one item is required")]
	public List<AdmissionStatusChangeItemDto> Items { get; set; } = new();
}

/// <summary>
/// Individual item for admission status change
/// </summary>
public class AdmissionStatusChangeItemDto
{
	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public StudentEnrollmentApprovalState Action { get; set; }

	public string? Remarks { get; set; }

	public bool SkipAccountProvisioning { get; set; }
}
