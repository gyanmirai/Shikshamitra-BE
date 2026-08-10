using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmReportScheduleService(AdmissionCrmReportScheduleWorkflow workflow)
	: ApplicationService, IAdmissionCrmReportScheduleService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public Task<AdmissionCrmReportScheduleDto> CreateReportScheduleAsync(Guid academicYearId, CreateAdmissionCrmReportScheduleDto input) => workflow.CreateAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public Task<IReadOnlyList<AdmissionCrmReportScheduleDto>> GetReportSchedulesAsync(Guid academicYearId) => workflow.GetAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public Task<AdmissionCrmReportScheduleDto> SetReportScheduleActiveAsync(Guid id, bool active) => workflow.SetActiveAsync(id, active);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public Task<AdmissionCrmReportScheduleRunResultDto> RunDueReportSchedulesAsync(Guid academicYearId) => workflow.RunDueAsync(academicYearId, true);
}
