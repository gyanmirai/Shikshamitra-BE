namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Represents the status of a student leave request
/// </summary>
public enum StudentLeaveStatusEnum
{
	/// <summary>
	/// Leave request is pending approval
	/// </summary>
	Pending = 0,

	/// <summary>
	/// Leave request has been approved
	/// </summary>
	Approved = 1,

	/// <summary>
	/// Leave request has been rejected
	/// </summary>
	Rejected = 2
}
