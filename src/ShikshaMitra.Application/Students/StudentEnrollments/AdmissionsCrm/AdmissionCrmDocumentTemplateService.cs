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
public class AdmissionCrmDocumentTemplateService(AdmissionCrmDocumentTemplateWorkflow workflow)
	: ApplicationService, IAdmissionCrmDocumentTemplateService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>> GetTemplatesAsync(Guid academicYearId, GetAdmissionCrmDocumentChecklistTemplateListInput input) =>
		workflow.GetTemplatesAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateAsync(Guid academicYearId, AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		workflow.CreateTemplateAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> UpdateTemplateAsync(Guid id, AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		workflow.UpdateTemplateAsync(id, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> PublishTemplateAsync(Guid id) => workflow.PublishTemplateAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateVersionAsync(Guid id) => workflow.CreateTemplateVersionAsync(id);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public Task<AdmissionCrmDocumentChecklistTemplateDto> RetireTemplateAsync(Guid id) => workflow.RetireTemplateAsync(id);
}
