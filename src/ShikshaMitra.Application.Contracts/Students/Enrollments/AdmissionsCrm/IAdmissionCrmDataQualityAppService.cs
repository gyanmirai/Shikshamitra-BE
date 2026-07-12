using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmDataQualityAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmDataQualityIssueDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmDataQualityIssuesInput input);
	Task<AdmissionCrmReconciliationResultDto> RunAsync(Guid academicYearId);
	Task<AdmissionCrmDataQualityIssueDto> AcknowledgeAsync(Guid id, AcknowledgeAdmissionCrmDataQualityIssueDto input);
	Task<AdmissionCrmDataQualityIssueDto> ResolveAsync(Guid id, ResolveAdmissionCrmDataQualityIssueDto input);
}
