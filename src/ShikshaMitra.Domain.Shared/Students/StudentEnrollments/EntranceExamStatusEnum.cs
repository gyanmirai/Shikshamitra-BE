namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Enum representing the status of a student's entrance exam
/// </summary>
public enum EntranceExamStatusEnum
{
	/// <summary>
	/// Exam scheduled but not taken yet
	/// </summary>
	Pending = 0,

	/// <summary>
	/// Student passed the exam
	/// </summary>
	Pass = 1,

	/// <summary>
	/// Student failed the exam
	/// </summary>
	Fail = 2,

	/// <summary>
	/// Student was absent for the exam
	/// </summary>
	Absent = 3
}
