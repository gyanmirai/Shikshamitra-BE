namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

// DTO for the create and Uodate LeavePolicy table records
public class CreateUpdateLeavePolicyDto
{
	public string LeaveType { get; set; }
	public int LeaveDays { get; set; }
	public bool CarryForward { get; set; }
	public AccrualRate AccrualRate { get; set; }
	public string Color { get; set; }
}