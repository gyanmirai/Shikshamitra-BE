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
public class AdmissionCrmCommunicationOptOutService(AdmissionCrmCommunicationOptOutWorkflow workflow)
	: ApplicationService, IAdmissionCrmCommunicationOptOutService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<PagedResultDto<AdmissionCrmCommunicationOptOutDto>> GetOptOutsAsync(Guid academicYearId, GetAdmissionCrmCommunicationOptOutListInput input) => workflow.GetAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmCommunicationOptOutDto> CreateOptOutAsync(Guid academicYearId, CreateAdmissionCrmCommunicationOptOutDto input) => workflow.CreateAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public Task<AdmissionCrmCommunicationOptOutDto> ClearOptOutAsync(Guid academicYearId, ClearAdmissionCrmCommunicationOptOutDto input) => workflow.ClearAsync(academicYearId, input);
}
