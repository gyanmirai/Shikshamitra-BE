namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveApprovalDto
{
	public EmployeeLeaveStatusType Status { get; set; }
	public string? RejectionReason { get; set; }
}