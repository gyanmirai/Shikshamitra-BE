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
		ApplicantName = applicantName,
		AdmissionCycleId = application.AdmissionCycleId,
		AdmissionOfferingId = application.AdmissionOfferingId,
		OfferingCode = offeringCode,
		CurrentStageName = currentStageName,
		Status = application.Status,
		DecisionReason = application.DecisionReason,
		DecisionBy = application.DecisionBy,
		DecisionAt = application.DecisionAt,
		OfferExpiresAt = application.OfferExpiresAt,
		OfferAcceptedAt = application.OfferAcceptedAt,
		EnrolledStudentId = application.EnrolledStudentId,
		StudentEnrollmentId = application.StudentEnrollmentId,
		CurrentStageInstanceId = application.CurrentStageInstanceId,
		LastSavedAt = application.LastSavedAt,
		SubmittedAt = application.SubmittedAt,
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
