using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmCycleConfigurationService : IApplicationService
{
	Task<AdmissionCrmCycleConfigurationDto> GetSchoolDefaultAsync();
	Task<AdmissionCrmCycleConfigurationDto> UpdateSchoolDefaultAsync(UpdateAdmissionCrmCycleConfigurationDto input);
	Task<AdmissionCrmCycleConfigurationDto> GetAsync(Guid academicYearId);
	Task<AdmissionCrmCycleConfigurationDto> UpdateAsync(Guid academicYearId, UpdateAdmissionCrmCycleConfigurationDto input);
	Task<AdmissionCrmCycleConfigurationDto> LockAsync(Guid academicYearId, AdmissionCrmJourneyLockDto input);
	Task<AdmissionCrmCycleConfigurationDto> UnlockAsync(Guid academicYearId, AdmissionCrmJourneyLockDto input);
}

public interface IAdmissionCrmPipelineConfigurationService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmPipelineStageDto>> GetAsync(Guid academicYearId);
	Task<AdmissionCrmPipelineStageDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmPipelineStageDto input);
	Task<AdmissionCrmPipelineStageDto> UpdateAsync(Guid stageId, UpdateAdmissionCrmPipelineStageDto input);
}

public interface IAdmissionCrmInquiryFormTemplateService : IApplicationService
{
	Task<AdmissionCrmFormTemplateDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmFormTemplateDto input);
	Task<PagedResultDto<AdmissionCrmFormTemplateDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmFormTemplateListInput input);
	Task<AdmissionCrmFormTemplateDto> GetAsync(Guid id);
	Task<AdmissionCrmFormTemplateDto> UpdateAsync(Guid id, UpdateAdmissionCrmFormTemplateDto input);
	Task<AdmissionCrmFormTemplateDto> UpdateStatusAsync(Guid id, UpdateAdmissionCrmFormTemplateStatusDto input);
}

public interface IAdmissionCrmMessageTemplateService : IApplicationService
{
	Task<AdmissionCrmMessageTemplateDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmMessageTemplateDto input);
	Task<PagedResultDto<AdmissionCrmMessageTemplateDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmMessageTemplateListInput input);
	Task<AdmissionCrmMessageTemplateDto> GetAsync(Guid id);
	Task<AdmissionCrmMessageTemplateDto> UpdateAsync(Guid id, UpdateAdmissionCrmMessageTemplateDto input);
	Task<AdmissionCrmMessageTemplateDto> UpdateStatusAsync(Guid id, UpdateAdmissionCrmMessageTemplateStatusDto input);
}
