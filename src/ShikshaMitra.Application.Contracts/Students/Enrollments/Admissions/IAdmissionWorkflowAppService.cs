using System;
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
	Task<AdmissionWorkflowDto> CloneAsync(Guid id);
	Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id);
	Task<AdmissionWorkflowDto> PublishAsync(Guid id);
}
