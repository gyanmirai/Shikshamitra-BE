using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmPolicyAdoptionService(
	AdmissionCrmPolicyAdoptionWorkflow workflow) : ApplicationService, IAdmissionCrmPolicyAdoptionService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AdoptPolicyPacks)]
	public Task<AdmissionCrmPolicyAdoptionDto> AdoptPolicyPackAsync(
		Guid academicYearId,
		Guid policyPackId,
		AdoptAdmissionCrmPolicyPackDto input) => workflow.AdoptAsync(academicYearId, policyPackId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks)]
	public Task<IReadOnlyList<AdmissionCrmPolicyAdoptionDto>> GetAdoptionsAsync(Guid academicYearId) =>
		workflow.GetListAsync(academicYearId);
}
