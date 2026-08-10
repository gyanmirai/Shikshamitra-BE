using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmEntranceExamMapper
{
	public static AdmissionCrmEntranceExamDto ToDto(AdmissionCrmEntranceExamRecord exam, AdmissionInquiryLead? lead) =>
		new()
		{
			Id = exam.Id, AcademicYearId = exam.AcademicYearId, LeadId = exam.LeadId,
			ApplicantNumber = lead?.ApplicantNumber ?? 0,
			ApplicantName = lead == null ? string.Empty : BuildApplicantName(lead),
			EntranceExamSlotId = exam.EntranceExamSlotId, SlotNameSnapshot = exam.SlotNameSnapshot,
			ExamStartTime = exam.ExamStartTime, ExamEndTime = exam.ExamEndTime, ExamRoomNo = exam.ExamRoomNo,
			CapacityOverrideUsed = exam.CapacityOverrideUsed, CapacityOverrideReason = exam.CapacityOverrideReason,
			ScheduledAt = exam.ScheduledAt, AttendanceStatus = exam.AttendanceStatus,
			AttendanceMarkedAt = exam.AttendanceMarkedAt, TotalMarks = exam.TotalMarks,
			PassingMarks = exam.PassingMarks, ObtainedMarks = exam.ObtainedMarks, Percentage = exam.Percentage,
			ExamStatus = exam.ExamStatus, ResultRecordedAt = exam.ResultRecordedAt, MeritRank = exam.MeritRank,
			EligibilityDecision = exam.EligibilityDecision,
			EligibilityDecisionReason = exam.EligibilityDecisionReason,
			EligibilityOverrideUsed = exam.EligibilityOverrideUsed,
			EligibilityOverrideReason = exam.EligibilityOverrideReason,
			EligibilityDecidedAt = exam.EligibilityDecidedAt
		};

	public static AdmissionCrmEligibilityHandoffDto ToHandoffDto(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord exam)
	{
		var missingFields = GetMissingHandoffFields(lead, exam);
		var canStart = exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible && missingFields.Count == 0;
		return new AdmissionCrmEligibilityHandoffDto
		{
			AcademicYearId = lead.AcademicYearId, LeadId = lead.Id, ExamRecordId = exam.Id,
			ApplicantNumber = lead.ApplicantNumber, CanStartAdmission = canStart,
			RequiresManualReview = exam.EligibilityOverrideUsed || !canStart,
			MissingAdmissionFields = missingFields, ApplicantName = BuildApplicantName(lead),
			DateOfBirth = lead.DateOfBirth, ParentGuardianName = lead.ParentGuardianName,
			PrimaryContactNo = lead.PrimaryContactNo, Email = lead.Email, AdmissionIntent = lead.AdmissionIntent,
			TargetStandardId = lead.TargetStandardId, DesiredClassName = lead.DesiredClassName,
			InquirySourceId = lead.InquirySourceId, InquirySourceSnapshot = lead.InquirySourceSnapshot,
			LeadStage = lead.Stage, AttendanceStatus = exam.AttendanceStatus, ExamStatus = exam.ExamStatus,
			TotalMarks = exam.TotalMarks, PassingMarks = exam.PassingMarks, ObtainedMarks = exam.ObtainedMarks,
			Percentage = exam.Percentage, MeritRank = exam.MeritRank,
			EligibilityDecision = exam.EligibilityDecision,
			EligibilityDecisionReason = exam.EligibilityDecisionReason,
			EligibilityOverrideUsed = exam.EligibilityOverrideUsed,
			EligibilityOverrideReason = exam.EligibilityOverrideReason,
			ResultRecordedAt = exam.ResultRecordedAt, EligibilityDecidedAt = exam.EligibilityDecidedAt
		};
	}

	public static void ApplyLeadStage(AdmissionInquiryLead lead, AdmissionCrmEntranceExamRecord exam)
	{
		var stage = exam.EligibilityDecision switch
		{
			AdmissionCrmEligibilityDecision.Eligible => AdmissionInquiryStage.Eligible,
			AdmissionCrmEligibilityDecision.Waitlisted => AdmissionInquiryStage.Waitlisted,
			AdmissionCrmEligibilityDecision.Rejected => AdmissionInquiryStage.Rejected,
			_ => exam.ResultRecordedAt.HasValue ? AdmissionInquiryStage.ExamCompleted : AdmissionInquiryStage.ExamScheduled
		};
		if (lead.Stage != stage) lead.ChangeStage(stage);
	}

	public static int? ParseApplicantNumber(string? value)
	{
		var normalized = value?.Trim().TrimStart('#');
		return int.TryParse(normalized, out var number) && number > 0 ? number : null;
	}

	private static List<string> GetMissingHandoffFields(
		AdmissionInquiryLead lead,
		AdmissionCrmEntranceExamRecord exam)
	{
		var missing = new List<string>();
		if (string.IsNullOrWhiteSpace(lead.FirstName) && string.IsNullOrWhiteSpace(lead.MiddleName) &&
		    string.IsNullOrWhiteSpace(lead.LastName)) missing.Add("Applicant name");
		if (string.IsNullOrWhiteSpace(lead.ParentGuardianName)) missing.Add("Parent or guardian name");
		if (string.IsNullOrWhiteSpace(lead.PrimaryContactNo) && string.IsNullOrWhiteSpace(lead.Email)) missing.Add("Contact");
		if (string.IsNullOrWhiteSpace(lead.AdmissionIntent)) missing.Add("Admission intent");
		if (!lead.TargetStandardId.HasValue && string.IsNullOrWhiteSpace(lead.DesiredClassName)) missing.Add("Desired class");
		if (!exam.ResultRecordedAt.HasValue && exam.ExamStatus is not EntranceExamStatusEnum.Absent) missing.Add("Entrance exam result");
		if (exam.EligibilityDecision == AdmissionCrmEligibilityDecision.Pending) missing.Add("Eligibility decision");
		return missing;
	}

	private static string BuildApplicantName(AdmissionInquiryLead lead)
	{
		var name = string.Join(" ", new[] { lead.FirstName, lead.MiddleName, lead.LastName }
			.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim())).Trim();
		return string.IsNullOrWhiteSpace(name)
			? lead.ParentGuardianName ?? lead.PrimaryContactNo ?? lead.Email ?? "Unnamed applicant"
			: name;
	}
}
