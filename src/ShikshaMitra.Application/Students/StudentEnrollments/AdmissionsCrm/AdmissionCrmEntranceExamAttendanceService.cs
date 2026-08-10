using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmEntranceExamAttendanceService(AdmissionCrmEntranceExamAttendanceWorkflow workflow)
	: ApplicationService, IAdmissionCrmEntranceExamAttendanceService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public Task<AdmissionCrmEntranceExamDto> MarkAttendanceAsync(
		Guid examRecordId,
		MarkAdmissionCrmExamAttendanceDto input) => workflow.MarkAsync(examRecordId, input);
}
