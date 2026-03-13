using System;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public class StudentStatusHistoryDto
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public StudentLifeCycleStatusEnum FromStatus { get; set; }
	public StudentLifeCycleStatusEnum ToStatus { get; set; }

	/// <summary>Maps <see cref="ToStatus"/> to the friendly operational label seen by end users.</summary>
	public StudentStatus OperationalStatus { get; set; }

	public string? Remarks { get; set; }

	/// <summary>ABP-populated: when the change was made.</summary>
	public DateTime CreationTime { get; set; }

	/// <summary>ABP-populated: ID of the user who made the change.</summary>
	public Guid? CreatorId { get; set; }
}
