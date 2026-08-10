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
public class AdmissionCrmDelegationService(AdmissionCrmDelegationWorkflow workflow) : ApplicationService, IAdmissionCrmDelegationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDelegation)]
	public Task<AdmissionCrmWorkDelegationDto> CreateDelegationAsync(Guid academicYearId, CreateAdmissionCrmWorkDelegationDto input) => workflow.CreateAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDelegation)]
	public Task<IReadOnlyList<AdmissionCrmWorkDelegationDto>> GetDelegationsAsync(Guid academicYearId) => workflow.GetAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDelegation)]
	public Task<AdmissionCrmWorkDelegationDto> CancelDelegationAsync(Guid id) => workflow.CancelAsync(id);
}
