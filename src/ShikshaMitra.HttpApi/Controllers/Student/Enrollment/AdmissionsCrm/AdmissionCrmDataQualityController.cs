using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmDataQualityController(IAdmissionCrmDataQualityAppService appService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/governance/data-quality")]
	public Task<PagedResultDto<AdmissionCrmDataQualityIssueDto>> GetListAsync(Guid academicYearId, [FromQuery] GetAdmissionCrmDataQualityIssuesInput input) => appService.GetListAsync(academicYearId, input);

	[HttpPost("{academicYearId}/governance/reconciliation/run")]
	public Task<AdmissionCrmReconciliationResultDto> RunAsync(Guid academicYearId) => appService.RunAsync(academicYearId);

	[HttpPost("governance/data-quality/{id}/acknowledge")]
	public Task<AdmissionCrmDataQualityIssueDto> AcknowledgeAsync(Guid id, [FromBody] AcknowledgeAdmissionCrmDataQualityIssueDto input) => appService.AcknowledgeAsync(id, input);

	[HttpPost("governance/data-quality/{id}/resolve")]
	public Task<AdmissionCrmDataQualityIssueDto> ResolveAsync(Guid id, [FromBody] ResolveAdmissionCrmDataQualityIssueDto input) => appService.ResolveAsync(id, input);
}
