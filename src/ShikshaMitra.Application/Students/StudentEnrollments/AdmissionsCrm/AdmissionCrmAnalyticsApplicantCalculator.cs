using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmAnalyticsApplicantCalculator
{
internal static AdmissionCrmExamAnalyticsDto BuildExamAnalytics(
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams,
		IReadOnlyCollection<AdmissionInquiryLead> leads,
		IReadOnlyCollection<AdmissionCrmExamSlot> slots)
	{
		var scheduledCount = exams.Count;
		var presentCount = exams.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Present);
		var passCount = exams.Count(x => x.ExamStatus == EntranceExamStatusEnum.Pass);
		var eligibleCount = exams.Count(x =>
			x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible);
		var waitlistedCount = exams.Count(x =>
			x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted);
		var rejectedCount = exams.Count(x =>
			x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected);
		var resultedCount = exams.Count(x =>
			x.ExamStatus is EntranceExamStatusEnum.Pass or EntranceExamStatusEnum.Fail);
		var percentages = exams
			.Where(x => x.Percentage.HasValue)
			.Select(x => x.Percentage!.Value)
			.ToList();
		var totalCapacity = slots.Sum(x => x.StudentLimit);
		var assignmentsBySlot = exams.GroupBy(x => x.EntranceExamSlotId).ToDictionary(x => x.Key, x => x.Count());

		return new AdmissionCrmExamAnalyticsDto
		{
			ScheduledCount = scheduledCount,
			PresentCount = presentCount,
			AbsentCount = exams.Count(x => x.AttendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or
				AdmissionCrmExamAttendanceStatus.NoShow or AdmissionCrmExamAttendanceStatus.Excused),
			NotMarkedCount = exams.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked),
			PassCount = passCount,
			FailCount = exams.Count(x => x.ExamStatus == EntranceExamStatusEnum.Fail),
			EligibleCount = eligibleCount,
			WaitlistedCount = waitlistedCount,
			RejectedCount = rejectedCount,
			MeritRankedCount = exams.Count(x => x.MeritRank.HasValue),
			CapacityOverrideCount = exams.Count(x => x.CapacityOverrideUsed),
			EligibilityOverrideCount = exams.Count(x => x.EligibilityOverrideUsed),
			AttendanceRate = Rate(presentCount, scheduledCount),
			PassRate = Rate(passCount, resultedCount),
			EligibilityRate = Rate(eligibleCount, scheduledCount),
			WaitlistRate = Rate(waitlistedCount, scheduledCount),
			RejectionRate = Rate(rejectedCount, scheduledCount),
			AveragePercentage = percentages.Count == 0 ? 0 : Math.Round((decimal)percentages.Average(), 2),
			HighestPercentage = percentages.Count == 0 ? null : Math.Round(percentages.Max(), 2),
			LowestPercentage = percentages.Count == 0 ? null : Math.Round(percentages.Min(), 2),
			SlotCount = slots.Count,
			TotalSlotCapacity = totalCapacity,
			OverCapacitySlotCount = slots.Count(x => assignmentsBySlot.GetValueOrDefault(x.Id) > x.StudentLimit),
			SlotUtilizationRate = Rate(scheduledCount, totalCapacity),
			ClassPerformance = BuildExamClassPerformance(exams, leads)
		};
	}

internal static AdmissionCrmRegistrationAnalyticsDto BuildRegistrationAnalytics(
		IReadOnlyCollection<AdmissionCrmRegistrationApplication> registrations,
		int totalLeadCount,
		DateTime now)
	{
		var issuedCount = registrations.Count;
		var openedCount = registrations.Count(x => x.OpenedAt.HasValue);
		var submittedCount = registrations.Count(x => x.SubmittedAt.HasValue);
		var verifiedCount = registrations.Count(x => x.VerifiedAt.HasValue);
		var admissionReadyCount = registrations.Count(x => x.Status == AdmissionCrmRegistrationStatus.AdmissionReady);
		var abandonedCount = registrations.Count(x =>
			x.Status is AdmissionCrmRegistrationStatus.Issued
				or AdmissionCrmRegistrationStatus.Opened
				or AdmissionCrmRegistrationStatus.Started
				or AdmissionCrmRegistrationStatus.PartiallyCompleted &&
			(now.Date - (x.LastSavedAt ?? x.IssuedAt).Date).Days >= 7);
		return new AdmissionCrmRegistrationAnalyticsDto
		{
			IssuedCount = issuedCount,
			OpenedCount = openedCount,
			StartedCount = registrations.Count(x => x.LastSavedAt.HasValue),
			SubmittedCount = submittedCount,
			VerifiedCount = verifiedCount,
			AdmissionReadyCount = admissionReadyCount,
			ExpiredCount = registrations.Count(x => x.Status == AdmissionCrmRegistrationStatus.Expired ||
				x.LinkExpiresAt <= now && x.Status is not AdmissionCrmRegistrationStatus.Verified and not AdmissionCrmRegistrationStatus.AdmissionReady),
			CorrectionPendingCount = registrations.Count(x => x.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection),
			AbandonedCount = abandonedCount,
			OpenRate = Rate(openedCount, issuedCount),
			CompletionRate = Rate(submittedCount, issuedCount),
			VerificationRate = Rate(verifiedCount, submittedCount),
			AdmissionYieldRate = Rate(admissionReadyCount, totalLeadCount),
			AverageCompletionPercentage = issuedCount == 0 ? 0 : Math.Round(registrations.Average(x => x.CompletionPercentage), 2)
		};
	}

internal static AdmissionCrmDocumentAnalyticsDto BuildDocumentAnalytics(
		IReadOnlyCollection<AdmissionCrmDocumentChecklistAssignment> checklists,
		IReadOnlyCollection<AdmissionCrmApplicantDocument> documents,
		DateTime now)
	{
		var currentDocuments = documents
			.Where(x => x.SupersededAt == null)
			.GroupBy(x => new { x.ChecklistAssignmentId, x.RequirementKey })
			.Select(x => x.OrderByDescending(document => document.Version).First())
			.ToList();
		var decidedDocuments = currentDocuments.Where(x => x.DecisionAt.HasValue).ToList();
		var reviewAges = decidedDocuments
			.Select(x => Math.Max(0, (x.DecisionAt!.Value - x.UploadedAt).TotalDays))
			.Concat(currentDocuments
				.Where(x => x.Status is AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview)
				.Select(x => Math.Max(0, (now - x.UploadedAt).TotalDays)))
			.ToList();
		return new AdmissionCrmDocumentAnalyticsDto
		{
			AssignedChecklistCount = checklists.Count,
			UploadedCount = currentDocuments.Count,
			UnderReviewCount = currentDocuments.Count(x => x.Status is AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview),
			VerifiedCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Verified),
			RejectedCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Rejected),
			WaivedCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Waived),
			OriginalSeenCount = currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.OriginalSeen),
			ReadinessConfirmedCount = checklists.Count(x => x.ReadinessConfirmedAt.HasValue),
			ReadinessOverrideCount = checklists.Count(x => x.ReadinessOverridden),
			RejectionRate = Rate(currentDocuments.Count(x => x.Status == AdmissionCrmApplicantDocumentStatus.Rejected), currentDocuments.Count),
			AverageReviewAgeDays = reviewAges.Count == 0 ? 0 : Math.Round((decimal)reviewAges.Average(), 2)
		};
	}

internal static IReadOnlyList<AdmissionCrmExamClassPerformanceDto> BuildExamClassPerformance(
		IReadOnlyCollection<AdmissionCrmEntranceExamRecord> exams,
		IReadOnlyCollection<AdmissionInquiryLead> leads)
	{
		var leadById = leads.ToDictionary(x => x.Id);

		return exams
			.GroupBy(exam =>
			{
				leadById.TryGetValue(exam.LeadId, out var lead);
				var className = string.IsNullOrWhiteSpace(lead?.DesiredClassName)
					? lead?.TargetStandardId?.ToString("D") ?? "Unassigned"
					: lead.DesiredClassName;
				return new
				{
					TargetStandardId = lead?.TargetStandardId,
					ClassName = className
				};
			})
			.Select(group =>
			{
				var scheduledCount = group.Count();
				var presentCount = group.Count(x => x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Present);
				var passCount = group.Count(x => x.ExamStatus == EntranceExamStatusEnum.Pass);
				var eligibleCount = group.Count(x =>
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible);
				var resultedCount = group.Count(x =>
					x.ExamStatus is EntranceExamStatusEnum.Pass or EntranceExamStatusEnum.Fail);
				var percentages = group
					.Where(x => x.Percentage.HasValue)
					.Select(x => x.Percentage!.Value)
					.ToList();

				return new AdmissionCrmExamClassPerformanceDto
				{
					TargetStandardId = group.Key.TargetStandardId,
					ClassName = group.Key.ClassName,
					ScheduledCount = scheduledCount,
					PresentCount = presentCount,
					AbsentCount = group.Count(x => x.AttendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or
						AdmissionCrmExamAttendanceStatus.NoShow or AdmissionCrmExamAttendanceStatus.Excused),
					PassCount = passCount,
					FailCount = group.Count(x => x.ExamStatus == EntranceExamStatusEnum.Fail),
					EligibleCount = eligibleCount,
					WaitlistedCount = group.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Waitlisted),
					RejectedCount = group.Count(x =>
						x.EligibilityDecision == AdmissionCrmEligibilityDecision.Rejected),
					MeritRankedCount = group.Count(x => x.MeritRank.HasValue),
					AttendanceRate = Rate(presentCount, scheduledCount),
					PassRate = Rate(passCount, resultedCount),
					EligibilityRate = Rate(eligibleCount, scheduledCount),
					AveragePercentage = percentages.Count == 0
						? 0
						: Math.Round((decimal)percentages.Average(), 2)
				};
			})
			.OrderByDescending(x => x.ScheduledCount)
			.ThenBy(x => x.ClassName)
			.ToList();
	}

internal static decimal Rate(int part, int total)
	{
		return total == 0 ? 0 : Math.Round(part * 100m / total, 2);
	}

internal static string ToDisplayLabel(string value)
	{
		return string.Concat(value.Select((character, index) =>
			index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
	}
}

