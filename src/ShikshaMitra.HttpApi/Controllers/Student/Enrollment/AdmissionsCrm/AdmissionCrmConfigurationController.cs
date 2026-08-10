using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmConfigurationController(
	IAdmissionCrmCycleConfigurationService cycleService,
	IAdmissionCrmPipelineConfigurationService pipelineService,
	IAdmissionCrmInquiryFormTemplateService formTemplateService,
	IAdmissionCrmMessageTemplateService messageTemplateService) : AbpControllerBase
{
	[HttpGet("configuration/cycle-default")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> GetSchoolDefaultCycleConfigurationAsync()
	{
		return await cycleService.GetSchoolDefaultAsync();
	}

	[HttpPut("configuration/cycle-default")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultCycleConfigurationAsync(
		[FromBody] UpdateAdmissionCrmCycleConfigurationDto input)
	{
		return await cycleService.UpdateSchoolDefaultAsync(input);
	}

	[HttpGet("{academicYearId}/configuration/cycle")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> GetCycleConfigurationAsync(
		[FromRoute] Guid academicYearId)
	{
		return await cycleService.GetAsync(academicYearId);
	}

	[HttpPut("{academicYearId}/configuration/cycle")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UpdateCycleConfigurationAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] UpdateAdmissionCrmCycleConfigurationDto input)
	{
		return await cycleService.UpdateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/configuration/pipeline-stages")]
	public async Task<PagedResultDto<AdmissionCrmPipelineStageDto>> GetPipelineStagesAsync(
		[FromRoute] Guid academicYearId)
	{
		return await pipelineService.GetAsync(academicYearId);
	}

	[HttpPost("{academicYearId}/configuration/pipeline-stages")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmPipelineStageDto> CreatePipelineStageAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmPipelineStageDto input)
	{
		return await pipelineService.CreateAsync(academicYearId, input);
	}

	[HttpPut("configuration/pipeline-stages/{stageId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmPipelineStageDto> UpdatePipelineStageAsync(
		[FromRoute] Guid stageId,
		[FromBody] UpdateAdmissionCrmPipelineStageDto input)
	{
		return await pipelineService.UpdateAsync(stageId, input);
	}

	[HttpPost("{academicYearId}/configuration/journey/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> LockJourneyAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] AdmissionCrmJourneyLockDto input)
	{
		return await cycleService.LockAsync(academicYearId, input);
	}

	[HttpPost("{academicYearId}/configuration/journey/unlock")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UnlockJourneyAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] AdmissionCrmJourneyLockDto input)
	{
		return await cycleService.UnlockAsync(academicYearId, input);
	}

	[HttpPost("{academicYearId}/configuration/form-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> CreateFormTemplateAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmFormTemplateDto input)
	{
		return await formTemplateService.CreateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/configuration/form-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<PagedResultDto<AdmissionCrmFormTemplateDto>> GetFormTemplatesAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmFormTemplateListInput input)
	{
		return await formTemplateService.GetListAsync(academicYearId, input);
	}

	[HttpGet("configuration/form-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> GetFormTemplateAsync([FromRoute] Guid id)
	{
		return await formTemplateService.GetAsync(id);
	}

	[HttpPut("configuration/form-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmFormTemplateDto input)
	{
		return await formTemplateService.UpdateAsync(id, input);
	}

	[HttpPatch("configuration/form-templates/{id}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateStatusAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmFormTemplateStatusDto input)
	{
		return await formTemplateService.UpdateStatusAsync(id, input);
	}

	[HttpPost("{academicYearId}/configuration/message-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> CreateMessageTemplateAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmMessageTemplateDto input)
	{
		return await messageTemplateService.CreateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/configuration/message-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmMessageTemplateDto>> GetMessageTemplatesAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmMessageTemplateListInput input)
	{
		return await messageTemplateService.GetListAsync(academicYearId, input);
	}

	[HttpGet("configuration/message-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> GetMessageTemplateAsync([FromRoute] Guid id)
	{
		return await messageTemplateService.GetAsync(id);
	}

	[HttpPut("configuration/message-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmMessageTemplateDto input)
	{
		return await messageTemplateService.UpdateAsync(id, input);
	}

	[HttpPatch("configuration/message-templates/{id}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateStatusAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmMessageTemplateStatusDto input)
	{
		return await messageTemplateService.UpdateStatusAsync(id, input);
	}
}
