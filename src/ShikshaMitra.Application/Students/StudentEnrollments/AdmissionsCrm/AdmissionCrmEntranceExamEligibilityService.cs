using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmEntranceExamEligibilityService(AdmissionCrmEntranceExamEligibilityWorkflow workflow)
	: ApplicationService, IAdmissionCrmEntranceExamEligibilityService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public Task<AdmissionCrmEntranceExamDto> DecideEligibilityAsync(
		Guid examRecordId,
		UpdateAdmissionCrmEligibilityDecisionDto input) => workflow.DecideAsync(examRecordId, input);
}
