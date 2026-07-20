using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmConfigurationAppService : IApplicationService
{
	Task<AdmissionCrmCycleConfigurationDto> GetSchoolDefaultCycleConfigurationAsync();
	Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultCycleConfigurationAsync(
		UpdateAdmissionCrmCycleConfigurationDto input);
	Task<AdmissionCrmCycleConfigurationDto> GetCycleConfigurationAsync(Guid academicYearId);
	Task<AdmissionCrmCycleConfigurationDto> UpdateCycleConfigurationAsync(
		Guid academicYearId,
		UpdateAdmissionCrmCycleConfigurationDto input);
	Task<PagedResultDto<AdmissionCrmPipelineStageDto>> GetPipelineStagesAsync(Guid academicYearId);
	Task<AdmissionCrmPipelineStageDto> CreatePipelineStageAsync(
		Guid academicYearId,
		CreateAdmissionCrmPipelineStageDto input);
	Task<AdmissionCrmPipelineStageDto> UpdatePipelineStageAsync(
		Guid stageId,
		UpdateAdmissionCrmPipelineStageDto input);
	Task<AdmissionCrmCycleConfigurationDto> LockJourneyAsync(Guid academicYearId, AdmissionCrmJourneyLockDto input);
	Task<AdmissionCrmCycleConfigurationDto> UnlockJourneyAsync(Guid academicYearId, AdmissionCrmJourneyLockDto input);
	Task<AdmissionCrmFormTemplateDto> CreateFormTemplateAsync(
		Guid academicYearId,
		CreateAdmissionCrmFormTemplateDto input);
	Task<PagedResultDto<AdmissionCrmFormTemplateDto>> GetFormTemplatesAsync(
		Guid academicYearId,
		GetAdmissionCrmFormTemplateListInput input);
	Task<AdmissionCrmFormTemplateDto> GetFormTemplateAsync(Guid id);
	Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateAsync(
		Guid id,
		UpdateAdmissionCrmFormTemplateDto input);
	Task<AdmissionCrmFormTemplateDto> UpdateFormTemplateStatusAsync(
		Guid id,
		UpdateAdmissionCrmFormTemplateStatusDto input);
	Task<AdmissionCrmMessageTemplateDto> CreateMessageTemplateAsync(
		Guid academicYearId,
		CreateAdmissionCrmMessageTemplateDto input);
	Task<PagedResultDto<AdmissionCrmMessageTemplateDto>> GetMessageTemplatesAsync(
		Guid academicYearId,
		GetAdmissionCrmMessageTemplateListInput input);
	Task<AdmissionCrmMessageTemplateDto> GetMessageTemplateAsync(Guid id);
	Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateAsync(
		Guid id,
		UpdateAdmissionCrmMessageTemplateDto input);
	Task<AdmissionCrmMessageTemplateDto> UpdateMessageTemplateStatusAsync(
		Guid id,
		UpdateAdmissionCrmMessageTemplateStatusDto input);
}
