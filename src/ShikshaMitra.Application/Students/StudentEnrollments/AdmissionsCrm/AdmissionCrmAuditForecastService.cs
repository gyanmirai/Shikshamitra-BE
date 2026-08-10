using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmAuditForecastService(AdmissionCrmAuditWorkflow auditWorkflow, AdmissionCrmForecastWorkflow forecastWorkflow)
	: ApplicationService, IAdmissionCrmAuditForecastService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAudit)]
	public Task<PagedResultDto<AdmissionCrmTimelineEventDto>> SearchAuditAsync(GetAdmissionCrmAuditInput input) => auditWorkflow.SearchAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ProtectedAnalytics)]
	public Task<AdmissionCrmForecastDto> GetAdvisoryForecastAsync(Guid academicYearId) => forecastWorkflow.GetAsync(academicYearId);
}
