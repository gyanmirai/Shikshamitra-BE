using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPublicProgressMapper
{
	internal static List<AdmissionApplicantStatusChecklistItemDto> BuildChecklist(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord? exam) =>
	[
		new()
		{
			Key = "inquiry-received", Label = "Inquiry received", Status = "Completed",
			Details = "Your inquiry has been recorded by the admissions team."
		},
		new()
		{
			Key = "follow-up", Label = "Admissions follow-up",
			Status = HasReachedFollowUp(lead.Stage) ? "Completed" : "Current",
			Details = HasReachedFollowUp(lead.Stage)
				? "The admissions team has started processing the inquiry."
				: "The admissions team will review the inquiry and contact you."
		},
		new()
		{
			Key = "entrance-exam-schedule", Label = "Entrance exam schedule",
			Status = exam is not null ? "Completed" : lead.Stage == AdmissionInquiryStage.ExamReady ? "Current" : "Pending",
			Details = exam is null ? "Exam schedule details will appear here when available." : FormatExamSchedule(exam)
		},
		new()
		{
			Key = "entrance-exam-result", Label = "Entrance exam result",
			Status = HasExamResult(exam) ? "Completed" : lead.Stage == AdmissionInquiryStage.ExamCompleted ? "Current" : "Pending",
			Details = HasExamResult(exam) ? FormatExamResult(exam!) : "Result details will appear after the entrance exam is reviewed."
		},
		new()
		{
			Key = "eligibility-decision", Label = "Eligibility decision",
			Status = HasEligibilityDecision(lead.Stage, exam) ? "Completed" : "Pending",
			Details = GetEligibilityDetails(lead.Stage, exam)
		}
	];

	internal static AdmissionApplicantStatusExamDto ToExamDto(AdmissionCrmEntranceExamRecord exam) => new()
	{
		Scheduled = true,
		SlotName = exam.SlotNameSnapshot,
		ExamStartTime = exam.ExamStartTime,
		ExamEndTime = exam.ExamEndTime,
		RoomNo = exam.ExamRoomNo,
		AttendanceStatus = exam.AttendanceStatus,
		ExamStatus = exam.ExamStatus,
		TotalMarks = exam.TotalMarks,
		PassingMarks = exam.PassingMarks,
		ObtainedMarks = exam.ObtainedMarks,
		Percentage = exam.Percentage,
		MeritRank = exam.MeritRank,
		EligibilityDecision = exam.EligibilityDecision,
		EligibilityDecisionReason = exam.EligibilityDecisionReason
	};

	internal static string GetNextStep(AdmissionInquiryStage stage) => stage switch
	{
		AdmissionInquiryStage.NewInquiry => "The admissions team will review the inquiry and follow up.",
		AdmissionInquiryStage.FollowUp => "The admissions team is following up on the inquiry.",
		AdmissionInquiryStage.ExamReady => "The inquiry is ready for entrance exam scheduling.",
		AdmissionInquiryStage.ExamScheduled => "The entrance exam has been scheduled.",
		AdmissionInquiryStage.ExamCompleted => "The entrance exam result is being reviewed.",
		AdmissionInquiryStage.Eligible => "The applicant is eligible for admission handoff.",
		AdmissionInquiryStage.Waitlisted => "The applicant is currently waitlisted.",
		AdmissionInquiryStage.Rejected => "The applicant is not eligible at this stage.",
		AdmissionInquiryStage.Stale => "The inquiry needs renewed follow-up.",
		AdmissionInquiryStage.Archived => "The inquiry is archived.",
		_ => "The admissions team will share the next step."
	};

	internal static string BuildDisplayName(AdmissionInquiryLead lead)
	{
		var name = $"{lead.FirstName} {lead.LastName}".Trim();
		return string.IsNullOrWhiteSpace(name) ? "Applicant" : name;
	}

	private static bool HasReachedFollowUp(AdmissionInquiryStage stage) => stage is
		AdmissionInquiryStage.FollowUp or AdmissionInquiryStage.ExamReady or AdmissionInquiryStage.ExamScheduled or
		AdmissionInquiryStage.ExamCompleted or AdmissionInquiryStage.Eligible or AdmissionInquiryStage.Waitlisted or
		AdmissionInquiryStage.Rejected;

	private static bool HasExamResult(AdmissionCrmEntranceExamRecord? exam) => exam is not null &&
		(exam.ResultRecordedAt.HasValue || exam.AttendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or
			AdmissionCrmExamAttendanceStatus.NoShow or AdmissionCrmExamAttendanceStatus.Excused);

	private static bool HasEligibilityDecision(AdmissionInquiryStage stage, AdmissionCrmEntranceExamRecord? exam) =>
		stage is AdmissionInquiryStage.Eligible or AdmissionInquiryStage.Waitlisted or AdmissionInquiryStage.Rejected ||
		exam is { EligibilityDecision: not AdmissionCrmEligibilityDecision.Pending };

	private static string FormatExamSchedule(AdmissionCrmEntranceExamRecord exam)
	{
		var name = string.IsNullOrWhiteSpace(exam.SlotNameSnapshot) ? "Entrance exam" : exam.SlotNameSnapshot;
		return $"{name} on {exam.ExamStartTime:dd MMM yyyy, h:mm tt} in room {exam.ExamRoomNo}.";
	}

	private static string FormatExamResult(AdmissionCrmEntranceExamRecord exam)
	{
		if (exam.AttendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or AdmissionCrmExamAttendanceStatus.NoShow or
			AdmissionCrmExamAttendanceStatus.Excused)
		{
			return exam.AttendanceStatus switch
			{
				AdmissionCrmExamAttendanceStatus.NoShow => "The applicant did not attend the entrance exam.",
				AdmissionCrmExamAttendanceStatus.Excused => "The applicant was excused from the entrance exam.",
				_ => "The applicant was marked absent for the entrance exam."
			};
		}

		if (!exam.ObtainedMarks.HasValue || !exam.TotalMarks.HasValue) return "The entrance exam result is being reviewed.";
		var percentage = exam.Percentage.HasValue ? $" ({exam.Percentage.Value:0.##}%)" : string.Empty;
		var rank = exam.MeritRank.HasValue ? $" Merit rank: {exam.MeritRank.Value}." : string.Empty;
		return $"{exam.ExamStatus}: {exam.ObtainedMarks.Value:0.##}/{exam.TotalMarks.Value:0.##}{percentage}.{rank}";
	}

	private static string GetEligibilityDetails(AdmissionInquiryStage stage, AdmissionCrmEntranceExamRecord? exam)
	{
		if (exam is { EligibilityDecision: not AdmissionCrmEligibilityDecision.Pending })
		{
			return exam.EligibilityDecisionReason ?? $"Decision: {exam.EligibilityDecision}.";
		}

		return stage switch
		{
			AdmissionInquiryStage.Eligible => "The applicant is eligible for admission handoff.",
			AdmissionInquiryStage.Waitlisted => "The applicant is currently waitlisted.",
			AdmissionInquiryStage.Rejected => "The applicant is not eligible at this stage.",
			_ => "Eligibility will be shown after entrance exam review."
		};
	}
}
