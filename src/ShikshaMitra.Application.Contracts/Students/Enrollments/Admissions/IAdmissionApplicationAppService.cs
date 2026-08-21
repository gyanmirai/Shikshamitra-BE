using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionApplicationAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input);
	Task<AdmissionApplicationDto> GetAsync(Guid id);
	Task<AdmissionApplicationDto> StartAsync(Guid applicantId, Guid offeringId, Guid? inquiryId = null);
	Task<AdmissionApplicationWorkflowDto> GetWorkflowAsync(Guid id);
	Task<AdmissionApplicationFormDto> GetFormAsync(Guid id);
	Task<AdmissionApplicationFormDto> UpdateFormValuesAsync(Guid id, UpdateAdmissionApplicationFormValuesDto input);
	Task<AdmissionApplicationSubmitResultDto> SubmitAsync(Guid id);
	Task<AdmissionApplicationFormDto> ReopenFormAsync(Guid id);
	Task<List<AdmissionFormChangeHistoryDto>> GetFormHistoryAsync(Guid id);
	Task<AdmissionApplicationWorkflowDto> AdvanceWorkflowAsync(Guid id, AdvanceAdmissionWorkflowDto input);
}
