namespace ShikshaMitra.Students.Attendance;

/// <summary>Leave request totals for the selected academic and search filters.</summary>
public class StudentLeaveStatusCountsDto
{
	public long PendingCount { get; set; }
	public long ApprovedCount { get; set; }
	public long RejectedCount { get; set; }
}
