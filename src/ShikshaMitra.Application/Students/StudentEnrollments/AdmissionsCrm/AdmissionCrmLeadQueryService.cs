using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
public class AdmissionCrmLeadQueryService(AdmissionCrmLeadQueryWorkflow workflow) : ApplicationService, IAdmissionCrmLeadQueryService
{
	public Task<AdmissionCrmLeadDto> GetAsync(Guid id) => workflow.GetAsync(id);
	public Task<PagedResultDto<AdmissionCrmLeadDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmLeadListInput input) =>
		workflow.GetListAsync(academicYearId, input);
	public Task<IReadOnlyList<AdmissionCrmLeadDuplicateDto>> PreviewDuplicatesAsync(
		Guid academicYearId,
		AdmissionCrmLeadDuplicatePreviewInput input) => workflow.PreviewDuplicatesAsync(academicYearId, input);
}