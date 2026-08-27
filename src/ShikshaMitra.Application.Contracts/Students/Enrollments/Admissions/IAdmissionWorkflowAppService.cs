using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionWorkflowAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionWorkflowDto>> GetListAsync(AdmissionWorkflowListInput input);
	Task<AdmissionWorkflowDto> GetAsync(Guid id);
	Task<AdmissionWorkflowDto> CreateAsync(CreateUpdateAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> UpdateAsync(Guid id, CreateUpdateAdmissionWorkflowDto input);
	Task DeleteAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionWorkflowDto> CloneAsync(Guid id, CloneAdmissionWorkflowDto input);
	Task<AdmissionWorkflowDto> CreateNewVersionAsync(Guid id);
	Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id);
	Task<AdmissionWorkflowDto> PublishAsync(Guid id, string? concurrencyStamp = null);
	Task<AdmissionWorkflowDto> ArchiveAsync(Guid id, string? concurrencyStamp = null);
	Task<List<AdmissionWorkflowDto>> GetVersionHistoryAsync(Guid id);
	Task<AdmissionWorkflowUsageDto> GetUsageAsync(Guid id);
	Task<IReadOnlyList<AdmissionWorkflowStageTypeMetadataDto>> GetStageTypesAsync();
}
