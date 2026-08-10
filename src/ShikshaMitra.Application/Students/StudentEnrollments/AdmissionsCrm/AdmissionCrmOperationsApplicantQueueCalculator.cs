using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmOperationsApplicantQueueCalculator
{
	private const int RegistrationExpiryWarningDays = 3;
	private const int RegistrationAbandonmentWarningDays = 7;
	private const int DocumentReviewWarningDays = 2;

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildUnassignedExamQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var scheduledLeadIds = data.Exams.Select(x => x.LeadId).ToHashSet();
		return data.Leads
			.Where(x => x.Stage == AdmissionInquiryStage.ExamReady && !scheduledLeadIds.Contains(x.Id))
			.Select(lead => AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(lead, $"exam:{lead.Id}", "Entrance exam needs assignment", "No exam slot has been assigned.", "Exam ready", null, AdmissionCrmOperationsFormat.AgeDays(lead.LastActivityTime, data.Now), true))
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildRegistrationInvitationPendingQueue(
		AdmissionCrmOperationsData data)
	{
		var registeredLeadIds = data.Registrations
			.Where(x => x.Status != AdmissionCrmRegistrationStatus.Revoked)
			.Select(x => x.LeadId)
			.ToHashSet();

		return data.Leads
			.Where(x => x.Stage == AdmissionInquiryStage.Eligible && !registeredLeadIds.Contains(x.Id))
			.Select(lead =>
			{
				var ageDays = AdmissionCrmOperationsFormat.AgeDays(lead.LastActivityTime, data.Now);
				return AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
					lead,
					$"registration-invitation:{lead.Id}",
					"Registration invitation is pending",
					"No current registration form is available. Choose a published template to continue.",
					"Ready to invite",
					null,
					ageDays,
					ageDays >= RegistrationAbandonmentWarningDays);
			})
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildCapacityConflictQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var slots = data.Slots.ToDictionary(x => x.Id);
		var results = new List<AdmissionCrmOperationsQueueItemDto>();
		foreach (var group in data.Exams.GroupBy(x => x.EntranceExamSlotId))
		{
			if (!slots.TryGetValue(group.Key, out var slot) || group.Count() <= slot.StudentLimit)
			{
				continue;
			}

			var exam = group.OrderBy(x => x.ScheduledAt).First();
			if (!leads.TryGetValue(exam.LeadId, out var lead))
			{
				continue;
			}

			results.Add(AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
				lead,
				$"capacity:{slot.Id}",
				$"{slot.SlotName ?? "Entrance exam slot"} is over capacity",
				$"{group.Count()} assignments for {slot.StudentLimit} seats.",
				"Capacity conflict",
				slot.StartDate,
				AdmissionCrmOperationsFormat.AgeDays(exam.ScheduledAt, data.Now),
				true,
				examRecordId: exam.Id));
		}
		return results;
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildNoShowQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		return data.Exams
			.Where(x => ((x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.Absent ||
						  x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NoShow) &&
						 x.EligibilityDecision == AdmissionCrmEligibilityDecision.Pending) ||
						(x.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked && x.ExamEndTime.UtcDateTime < data.Now))
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(exam => AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
				leads[exam.LeadId],
				$"no-show:{exam.Id}",
				exam.AttendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or AdmissionCrmExamAttendanceStatus.NoShow
					? "Entrance exam no-show"
					: "Attendance is overdue",
				$"{exam.SlotNameSnapshot ?? "Exam"} was scheduled for {exam.ExamStartTime:dd MMM yyyy, h:mm tt}.",
				exam.AttendanceStatus.ToString(),
				exam.ExamEndTime,
				AdmissionCrmOperationsFormat.AgeDays(exam.ExamEndTime.UtcDateTime, data.Now),
				true,
				examRecordId: exam.Id))
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildExpiringLinkQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var warningAt = data.Now.AddDays(RegistrationExpiryWarningDays);
		return data.Registrations
			.Where(x => AdmissionCrmOperationsFormat.IsRegistrationOpen(x.Status) && x.LinkExpiresAt <= warningAt)
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(application => AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
				leads[application.LeadId],
				$"expiry:{application.Id}",
				application.LinkExpiresAt <= data.Now ? "Registration link expired" : "Registration link expiring",
				$"Form {application.FormNumber} expires {application.LinkExpiresAt:dd MMM yyyy, h:mm tt}.",
				application.Status.ToString(),
				AdmissionCrmOperationsFormat.ToOffset(application.LinkExpiresAt),
				AdmissionCrmOperationsFormat.AgeDays(application.IssuedAt, data.Now),
				application.LinkExpiresAt <= data.Now.AddDays(1),
				registrationApplicationId: application.Id))
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildPendingPaperEntryQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		return data.Registrations
			.Where(x => x.Provenance == AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper)
			.Where(x => x.DataEntryState is AdmissionCrmRegistrationDataEntryState.NotStarted or AdmissionCrmRegistrationDataEntryState.InProgress)
			.Where(x => x.Status is not (AdmissionCrmRegistrationStatus.Submitted
				or AdmissionCrmRegistrationStatus.Resubmitted
				or AdmissionCrmRegistrationStatus.Verified
				or AdmissionCrmRegistrationStatus.AdmissionReady
				or AdmissionCrmRegistrationStatus.Revoked
				or AdmissionCrmRegistrationStatus.Withdrawn))
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(application =>
			{
				var received = application.PhysicalFormReceivedDate.HasValue
					? $" received {application.PhysicalFormReceivedDate.Value:dd MMM yyyy}"
					: string.Empty;
				var form = string.IsNullOrWhiteSpace(application.PhysicalFormNumber)
					? application.FormNumber
					: application.PhysicalFormNumber;
				return AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
					leads[application.LeadId],
					$"paper-entry:{application.Id}",
					"Enter paper registration responses",
					$"Physical form {form}{received} is waiting for staff data entry.",
					application.DataEntryState.ToString(),
					null,
					AdmissionCrmOperationsFormat.AgeDays(application.PhysicalFormReceivedDate ?? application.IssuedAt, data.Now),
					AdmissionCrmOperationsFormat.AgeDays(application.PhysicalFormReceivedDate ?? application.IssuedAt, data.Now) >= RegistrationAbandonmentWarningDays,
					registrationApplicationId: application.Id);
			})
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildIncompleteRegistrationQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		return data.Registrations
			.Where(x => AdmissionCrmOperationsFormat.IsRegistrationOpen(x.Status))
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(application => AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
				leads[application.LeadId],
				$"incomplete:{application.Id}",
				"Registration is incomplete",
				$"Form {application.FormNumber} is {application.CompletionPercentage:0}% complete.",
				application.Status.ToString(),
				AdmissionCrmOperationsFormat.ToOffset(application.LinkExpiresAt),
				AdmissionCrmOperationsFormat.AgeDays(application.LastSavedAt ?? application.IssuedAt, data.Now),
				AdmissionCrmOperationsFormat.AgeDays(application.LastSavedAt ?? application.IssuedAt, data.Now) >= RegistrationAbandonmentWarningDays,
				registrationApplicationId: application.Id))
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildDocumentQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads,
		AdmissionCrmApplicantDocumentStatus status)
	{
		var registrationsByAssignment = data.Checklists.ToDictionary(x => x.Id, x => x.RegistrationApplicationId);
		return AdmissionCrmOperationsDecisionQueueCalculator.GetCurrentDocuments(data.Documents)
			.Where(x => x.Status == status && leads.ContainsKey(x.LeadId))
			.Select(document => AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
				leads[document.LeadId],
				$"document:{document.Id}",
				$"{document.RequirementKey} requires correction",
				document.DecisionReason,
				document.Status.ToString(),
				null,
				AdmissionCrmOperationsFormat.AgeDays(document.DecisionAt ?? document.UploadedAt, data.Now),
				AdmissionCrmOperationsFormat.AgeDays(document.DecisionAt ?? document.UploadedAt, data.Now) >= DocumentReviewWarningDays,
				registrationApplicationId: registrationsByAssignment.GetValueOrDefault(document.ChecklistAssignmentId),
				documentId: document.Id,
				checklistAssignmentId: document.ChecklistAssignmentId))
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildDocumentReviewQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var registrationsByAssignment = data.Checklists.ToDictionary(x => x.Id, x => x.RegistrationApplicationId);
		return AdmissionCrmOperationsDecisionQueueCalculator.GetCurrentDocuments(data.Documents)
			.Where(x => x.Status is AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview)
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(document => AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
				leads[document.LeadId],
				$"review:{document.Id}",
				$"Review {document.RequirementKey}",
				document.FileName,
				document.Status.ToString(),
				null,
				AdmissionCrmOperationsFormat.AgeDays(document.UploadedAt, data.Now),
				AdmissionCrmOperationsFormat.AgeDays(document.UploadedAt, data.Now) >= DocumentReviewWarningDays,
				registrationApplicationId: registrationsByAssignment.GetValueOrDefault(document.ChecklistAssignmentId),
				documentId: document.Id,
				checklistAssignmentId: document.ChecklistAssignmentId))
			.ToList();
	}

	internal static List<AdmissionCrmOperationsQueueItemDto> BuildAdmissionReadyQueue(
		AdmissionCrmOperationsData data,
		IReadOnlyDictionary<Guid, AdmissionInquiryLead> leads)
	{
		var registrations = data.Registrations.ToDictionary(x => x.Id);
		return data.Checklists
			.Where(x => x.ReadinessConfirmedAt.HasValue && registrations.TryGetValue(x.RegistrationApplicationId, out var registration) && registration.Status == AdmissionCrmRegistrationStatus.AdmissionReady)
			.Where(x => leads.ContainsKey(x.LeadId))
			.Select(checklist =>
			{
				var registration = registrations[checklist.RegistrationApplicationId];
				return AdmissionCrmOperationsDecisionQueueCalculator.CreateItem(
					leads[checklist.LeadId],
					$"ready:{registration.Id}",
					"Admission handoff is ready",
					$"Form {registration.FormNumber} passed registration and document verification.",
					registration.Status.ToString(),
					null,
					AdmissionCrmOperationsFormat.AgeDays(checklist.ReadinessConfirmedAt!.Value, data.Now),
					false,
					registrationApplicationId: registration.Id,
					checklistAssignmentId: checklist.Id);
			})
			.ToList();
	}
}