using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmDocumentReviewService(AdmissionCrmDocumentReviewWorkflow workflow)
	: ApplicationService, IAdmissionCrmDocumentReviewService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments)]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromStaffAsync(Guid registrationApplicationId, ProcessedAdmissionCrmApplicantDocumentUploadDto input) =>
		workflow.UploadFromStaffAsync(registrationApplicationId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments)]
	public Task<AdmissionCrmApplicantDocumentDto> ReviewAsync(Guid documentId, ReviewAdmissionCrmApplicantDocumentDto input) =>
		workflow.ReviewAsync(documentId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveDocuments)]
	public Task<AdmissionCrmApplicantDocumentDto> WaiveAsync(Guid assignmentId, string requirementKey, AdmissionCrmDocumentReasonDto input) =>
		workflow.WaiveAsync(assignmentId, requirementKey, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmOriginalSeen)]
	public Task<AdmissionCrmApplicantDocumentDto> MarkOriginalSeenAsync(Guid documentId) => workflow.MarkOriginalSeenAsync(documentId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness)]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> ConfirmReadinessAsync(Guid assignmentId, ConfirmAdmissionCrmReadinessDto input) =>
		workflow.ConfirmReadinessAsync(assignmentId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments)]
	public Task<AdmissionCrmApplicantDocumentDownloadDto> DownloadAsync(Guid documentId) => workflow.DownloadAsync(documentId);
}
