using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public class ChangeStudentStatusDto
{
	/// <summary>
	/// The new operational status for the student.
	/// <list type="bullet">
	///   <item><see cref="StudentStatus.Active"/> – student is enrolled and attending.</item>
	///   <item><see cref="StudentStatus.Inactive"/> – student is temporarily inactive (e.g. on leave).</item>
	///   <item><see cref="StudentStatus.Left"/> – student has permanently left the institution.</item>
	/// </list>
	/// </summary>
	[Required]
	public StudentStatus Status { get; set; }

	/// <summary>
	/// Free-text reason for the status change. Stored in the audit log and is
	/// required when marking a student as <see cref="StudentStatus.Left"/>.
	/// </summary>
	[MaxLength(1000)]
	public string? Remarks { get; set; }
}
