using Volo.Abp;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Raised when a teacher tries to resubmit a lesson plan without addressing requested changes.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Roslynator",
	"RCS1194:Implement exception constructors",
	Justification = "This domain exception intentionally has a fixed error code and user-facing message.")]
public sealed class LessonPlanRevisionRequiredException : BusinessException
{
	public LessonPlanRevisionRequiredException()
		: base(
			CurriculumErrorCodes.LessonPlanRevisionRequired,
			"No changes detected. Please modify the lesson plan to address the reviewer's comments before resubmitting.")
	{
	}
}
