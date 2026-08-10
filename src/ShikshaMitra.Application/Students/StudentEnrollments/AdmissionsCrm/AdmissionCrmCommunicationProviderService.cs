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
public class AdmissionCrmCommunicationProviderService(AdmissionCrmCommunicationProviderWorkflow workflow)
	: ApplicationService, IAdmissionCrmCommunicationProviderService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<PagedResultDto<AdmissionCrmCommunicationProviderDto>> GetProvidersAsync(Guid academicYearId) => workflow.GetAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public Task<AdmissionCrmCommunicationProviderDto> UpdateProviderAsync(Guid academicYearId, AdmissionCrmMessageChannel channel,
		UpdateAdmissionCrmCommunicationProviderDto input) => workflow.UpdateAsync(academicYearId, channel, input);
}
