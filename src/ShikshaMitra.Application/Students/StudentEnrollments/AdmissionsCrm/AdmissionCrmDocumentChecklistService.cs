using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmDocumentChecklistService(AdmissionCrmDocumentChecklistWorkflow workflow)
	: ApplicationService, IAdmissionCrmDocumentChecklistService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> AssignChecklistAsync(Guid registrationApplicationId, AssignAdmissionCrmDocumentChecklistDto input) =>
		workflow.AssignChecklistAsync(registrationApplicationId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmApplicantDocumentChecklistDto> GetChecklistAsync(Guid registrationApplicationId) =>
		workflow.GetChecklistAsync(registrationApplicationId);
}
