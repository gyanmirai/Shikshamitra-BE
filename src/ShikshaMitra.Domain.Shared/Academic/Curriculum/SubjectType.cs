namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Type of subject assignment
/// </summary>
public enum SubjectType
{
	/// <summary>Mandatory subject for all students</summary>
	Core = 0,

	/// <summary>Student can choose from a group</summary>
	Elective = 1,

	/// <summary>Choice-based, not mandatory</summary>
	Optional = 2,

	/// <summary>Co-curricular / extra-curricular activity</summary>
	CoCurricular = 3,

	/// <summary>Language subject (e.g., Hindi, Gujarati, French)</summary>
	Language = 4,

	/// <summary>Skill-based subject (e.g., Coding, Pottery)</summary>
	SkillBased = 5,

	/// <summary>Activity-based subject (e.g., Sports, Art)</summary>
	Activity = 6
}
