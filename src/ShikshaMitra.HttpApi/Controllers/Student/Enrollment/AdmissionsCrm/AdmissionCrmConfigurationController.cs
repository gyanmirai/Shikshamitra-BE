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
	IAdmissionCrmConfigurationAppService admissionCrmConfigurationAppService) : AbpControllerBase
{
	[HttpGet("configuration/cycle-default")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> GetSchoolDefaultCycleConfigurationAsync()
	{
		return await admissionCrmConfigurationAppService.GetSchoolDefaultCycleConfigurationAsync();
	}

	[HttpPut("configuration/cycle-default")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultCycleConfigurationAsync(
		[FromBody] UpdateAdmissionCrmCycleConfigurationDto input)
	{
		return await admissionCrmConfigurationAppService.UpdateSchoolDefaultCycleConfigurationAsync(input);
	}

	[HttpGet("{academicYearId}/configuration/cycle")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> GetCycleConfigurationAsync(
		[FromRoute] Guid academicYearId)
	{
		return await admissionCrmConfigurationAppService.GetCycleConfigurationAsync(academicYearId);
	}

	[HttpPut("{academicYearId}/configuration/cycle")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UpdateCycleConfigurationAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] UpdateAdmissionCrmCycleConfigurationDto input)
	{
		return await admissionCrmConfigurationAppService.UpdateCycleConfigurationAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/configuration/pipeline-stages")]
	public async Task<PagedResultDto<AdmissionCrmPipelineStageDto>> GetPipelineStagesAsync(
		[FromRoute] Guid academicYearId)
	{
		return await admissionCrmConfigurationAppService.GetPipelineStagesAsync(academicYearId);
	}

	[HttpPost("{academicYearId}/configuration/pipeline-stages")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmPipelineStageDto> CreatePipelineStageAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmPipelineStageDto input)
	{
		return await admissionCrmConfigurationAppService.CreatePipelineStageAsync(academicYearId, input);
	}

	[HttpPut("configuration/pipeline-stages/{stageId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmPipelineStageDto> UpdatePipelineStageAsync(
		[FromRoute] Guid stageId,
		[FromBody] UpdateAdmissionCrmPipelineStageDto input)
	{
		return await admissionCrmConfigurationAppService.UpdatePipelineStageAsync(stageId, input);
	}

	[HttpPost("{academicYearId}/configuration/journey/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> LockJourneyAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] AdmissionCrmJourneyLockDto input)
	{
		return await admissionCrmConfigurationAppService.LockJourneyAsync(academicYearId, input);
	}

	[HttpPost("{academicYearId}/configuration/journey/unlock")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleConfigurationDto> UnlockJourneyAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] AdmissionCrmJourneyLockDto input)
	{
		return await admissionCrmConfigurationAppService.UnlockJourneyAsync(academicYearId, input);
	}

	[HttpPost("{academicYearId}/configuration/form-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> CreateFormTemplateAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmFormTemplateDto input)
	{
		return await admissionCrmConfigurationAppService.CreateFormTemplateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/configuration/form-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<PagedResultDto<AdmissionCrmFormTemplateDto>> GetFormTemplatesAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmFormTemplateListInput input)
	{
		return await admissionCrmConfigurationAppService.GetFormTemplatesAsync(academicYearId, input);
	}

	[HttpGet("configuration/form-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> GetFormTemplateAsync([FromRoute] Guid id)
	{
		return await admissionCrmConfigurationAppService.GetFormTemplateAsync(id);
	}

	[HttpPut("configuration/form-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmFormTemplateDto input)
	{
		return await admissionCrmConfigurationAppService.UpdateFormTemplateAsync(id, input);
	}

	[HttpPatch("configuration/form-templates/{id}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateStatusAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmFormTemplateStatusDto input)
	{
		return await admissionCrmConfigurationAppService.UpdateFormTemplateStatusAsync(id, input);
	}

	[HttpPost("{academicYearId}/configuration/message-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> CreateMessageTemplateAsync(
		[FromRoute] Guid academicYearId,
		[FromBody] CreateAdmissionCrmMessageTemplateDto input)
	{
		return await admissionCrmConfigurationAppService.CreateMessageTemplateAsync(academicYearId, input);
	}

	[HttpGet("{academicYearId}/configuration/message-templates")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<PagedResultDto<AdmissionCrmMessageTemplateDto>> GetMessageTemplatesAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmMessageTemplateListInput input)
	{
		return await admissionCrmConfigurationAppService.GetMessageTemplatesAsync(academicYearId, input);
	}

	[HttpGet("configuration/message-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> GetMessageTemplateAsync([FromRoute] Guid id)
	{
		return await admissionCrmConfigurationAppService.GetMessageTemplateAsync(id);
	}

	[HttpPut("configuration/message-templates/{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmMessageTemplateDto input)
	{
		return await admissionCrmConfigurationAppService.UpdateMessageTemplateAsync(id, input);
	}

	[HttpPatch("configuration/message-templates/{id}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageTemplates)]
	public async Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateStatusAsync(
		[FromRoute] Guid id,
		[FromBody] UpdateAdmissionCrmMessageTemplateStatusDto input)
	{
		return await admissionCrmConfigurationAppService.UpdateMessageTemplateStatusAsync(id, input);
	}
}
