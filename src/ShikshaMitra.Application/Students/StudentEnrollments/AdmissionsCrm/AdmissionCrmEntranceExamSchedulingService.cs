using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmEntranceExamSchedulingService(AdmissionCrmEntranceExamSchedulingWorkflow workflow)
	: ApplicationService, IAdmissionCrmEntranceExamSchedulingService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam)]
	public Task<AdmissionCrmEntranceExamDto> ScheduleAsync(Guid leadId, ScheduleAdmissionCrmEntranceExamDto input) =>
		workflow.ScheduleAsync(leadId, input);
}
