using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// DTO for approving or rejecting a student leave request
/// </summary>
public class ApproveRejectStudentLeaveDto
{
	[Required]
	public StudentLeaveStatusEnum Status { get; set; }

	[MaxLength(StudentLeaveConsts.MaxRemarksLength)]
	public string? Remarks { get; set; }

	[MaxLength(StudentLeaveConsts.MaxRejectionReasonLength)]
	public string? RejectionReason { get; set; }
}
