using System;

namespace ShikshaMitra.Academic.StudentAcademic360;

public partial class StudentAcademic360AppService
{
	private static string ResolveTaskFilterCategory(StudentAcademic360LearningTaskDto task)
	{
		var status = task.Status.Trim().ToLowerInvariant();
		if (task.Title.ToLowerInvariant().Contains("resubmi", StringComparison.Ordinal) ||
			status.Contains("resubmi", StringComparison.Ordinal)) return "RESUBMISSION_REQUIRED";
		if (status.Contains("late", StringComparison.Ordinal) || status.Contains("overdue", StringComparison.Ordinal)) return "LATE";
		if (status.Contains("evaluation", StringComparison.Ordinal) || status.Contains("review", StringComparison.Ordinal) ||
			(status == "submitted" && !task.Marks.HasValue)) return "EVALUATION_PENDING";
		if (status.Contains("evaluated", StringComparison.Ordinal) || status.Contains("graded", StringComparison.Ordinal) ||
			task.Marks >= 0) return "EVALUATED";
		return status.Contains("submitted", StringComparison.Ordinal) ? "SUBMITTED" : "PENDING";
	}
}
