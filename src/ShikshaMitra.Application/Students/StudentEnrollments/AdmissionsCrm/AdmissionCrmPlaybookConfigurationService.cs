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
public class AdmissionCrmPlaybookConfigurationService(AdmissionCrmPlaybookConfigurationWorkflow workflow)
	: ApplicationService, IAdmissionCrmPlaybookConfigurationService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public Task<AdmissionCrmPlaybookDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmPlaybookDto input) => workflow.CreateAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmPlaybookDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmPlaybookListInput input) => workflow.GetListAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmPlaybookDto> GetAsync(Guid id) => workflow.GetAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public Task<AdmissionCrmPlaybookDto> UpdateAsync(Guid id, UpdateAdmissionCrmPlaybookDto input) => workflow.UpdateAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishPlaybooks)]
	public Task<AdmissionCrmPlaybookDto> PublishAsync(Guid id) => workflow.PublishAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public Task<AdmissionCrmPlaybookDto> CreateNewVersionAsync(Guid id) => workflow.CreateNewVersionAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishPlaybooks)]
	public Task<AdmissionCrmPlaybookDto> RetireAsync(Guid id) => workflow.RetireAsync(id);
}
