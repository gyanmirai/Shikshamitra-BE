using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmEntranceExamReadService(AdmissionCrmEntranceExamReadWorkflow workflow)
	: ApplicationService, IAdmissionCrmEntranceExamReadService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmEntranceExamDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmEntranceExamListInput input) => workflow.GetListAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmEntranceExamDto> GetByLeadAsync(Guid leadId) => workflow.GetByLeadAsync(leadId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmEligibilityHandoffDto> GetEligibilityHandoffAsync(Guid examRecordId) =>
		workflow.GetEligibilityHandoffAsync(examRecordId);
}
