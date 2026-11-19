using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionApplicationMapper
{
	public static AdmissionApplicationDto ToDto(
		AdmissionApplication application,
		string applicantName = "",
		string offeringCode = "",
		string currentStageName = "") => new()
	{
		Id = application.Id,
		ConcurrencyStamp = application.ConcurrencyStamp,
		ApplicationNumber = application.ApplicationNumber,
		ApplicantId = application.ApplicantId,
		InquiryId = application.InquiryId,
		ApplicantName = applicantName,
		AdmissionCycleId = application.AdmissionCycleId,
		AdmissionOfferingId = application.AdmissionOfferingId,
		AssignedCounsellorId = application.AssignedCounsellorId,
		OfferingCode = offeringCode,
		CurrentStageName = currentStageName,
		Status = application.Status,
		CanEndJourney = application.CanEndJourney,
		DecisionReason = application.DecisionReason,
		DecisionBy = application.DecisionBy,
		DecisionAt = AdmissionEventUtcTime.Normalize(application.DecisionAt),
		OfferExpiresAt = application.OfferExpiresAt,
		OfferAcceptedAt = AdmissionEventUtcTime.Normalize(application.OfferAcceptedAt),
		StudentId = application.StudentId,
		OnboardingCaseId = application.OnboardingCaseId,
		FinalizedAt = AdmissionEventUtcTime.Normalize(application.FinalizedAt),
		EnrolledStudentId = application.EnrolledStudentId,
		StudentEnrollmentId = application.StudentEnrollmentId,
		CurrentStageInstanceId = application.CurrentStageInstanceId,
		LastSavedAt = AdmissionEventUtcTime.Normalize(application.LastSavedAt),
		SubmittedAt = AdmissionEventUtcTime.Normalize(application.SubmittedAt),
		CreationTime = application.CreationTime,
		CreatorId = application.CreatorId,
		LastModificationTime = application.LastModificationTime,
		LastModifierId = application.LastModifierId
	};

	public static object? GetBoundValue(
		string key,
		AdmissionApplicant applicant,
		AdmissionGuardian? guardian) => AdmissionFormBindingRegistry.GetValue(key, applicant, guardian);
}
