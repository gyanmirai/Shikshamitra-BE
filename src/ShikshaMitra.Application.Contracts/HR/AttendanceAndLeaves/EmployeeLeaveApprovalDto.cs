namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeaveApprovalDto
{
	public EmployeeLeaveStatusType Status { get; set; }
	public string? RejectionReason { get; set; }
}