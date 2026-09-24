using Volo.Abp;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Raised when a teacher tries to change a terminal lesson plan.
/// Approved and rejected plans are retained as immutable review records.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Roslynator",
	"RCS1194:Implement exception constructors",
	Justification = "This domain exception intentionally has a fixed error code and status-specific user-facing message.")]
public sealed class LessonPlanNotEditableException : BusinessException
{
	public LessonPlanNotEditableException(LessonPlanStatus status)
		: base(CurriculumErrorCodes.LessonPlanNotEditable, GetMessage(status))
	{
		WithData("Status", status.ToString());
	}

	private static string GetMessage(LessonPlanStatus status) => status switch
	{
		LessonPlanStatus.Rejected =>
			"Rejected lesson plans are locked. Create a new lesson plan instead.",
		LessonPlanStatus.Approved =>
			"Approved lesson plans are locked and cannot be modified.",
		_ => "This lesson plan is locked and cannot be modified."
	};
}
