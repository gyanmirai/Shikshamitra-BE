using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>One display projection for roster cells, server filters and progress counts. Never changes assessment evidence or routing.</summary>
internal sealed record AdmissionAssessmentRosterState(string Status, string Attendance, string Progress)
{
	internal static AdmissionAssessmentRosterState Resolve(AdmissionAssessmentAttempt? attempt,
		AdmissionAssessmentSession? session, DateTime now, bool scoringEnabled, bool attendanceRequired)
	{
		var startsAt = attempt?.SlotStartsAt ?? session?.StartsAt;
		var endsAt = attempt?.SlotEndsAt ?? session?.EndsAt;
		var attendance = attempt?.Attendance is "present" or "absent" ? attempt.Attendance
			: attempt == null || attempt.Status == "cancelled" ? "notApplicable"
			: attempt.Outcome == "waived" || !attendanceRequired ? "notRequired"
			: startsAt > now ? "notMarked" : "pending";
		AdmissionAssessmentRosterState State(string status, string progress) => new(status, attendance, progress);
		if (attempt == null || attempt.Status == "cancelled") return State("notBooked", "pending");
		if (attempt.Outcome == "waived") return State("waived", "complete");
		if (attempt.Outcome == "retake") return State("retakeRequired", "pending");
		if (startsAt > now) return State("scheduled", "pending");
		if (attempt.Status == "pendingReview") return State("reviewPending", "ongoing");
		if (attendanceRequired && attempt.Attendance == "pending") return State("attendancePending", "ongoing");
		var recorded = attempt.Status == "completed" &&
			(attempt.Outcome == "absent" || attempt.Outcome is "passed" or "failed" && (!scoringEnabled || attempt.Marks.HasValue));
		if (recorded) return !endsAt.HasValue || endsAt <= now
			? State(attempt.Outcome, "complete") : State("ongoing", "ongoing");
		return State("resultPending", "ongoing");
	}
}
