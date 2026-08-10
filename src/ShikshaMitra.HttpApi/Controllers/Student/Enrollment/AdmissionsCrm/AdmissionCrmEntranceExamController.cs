using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmEntranceExamController(
	IAdmissionCrmEntranceExamReadService readService,
	IAdmissionCrmEntranceExamSchedulingService schedulingService,
	IAdmissionCrmEntranceExamAttendanceService attendanceService,
	IAdmissionCrmEntranceExamResultService resultService,
	IAdmissionCrmEntranceExamEligibilityService eligibilityService) : AbpControllerBase
{
	[HttpGet("{academicYearId}/exams")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmEntranceExamDto>> GetListAsync(
		[FromRoute] Guid academicYearId,
		[FromQuery] GetAdmissionCrmEntranceExamListInput input)
	{
		return await readService.GetListAsync(academicYearId, input);
	}

	[HttpGet("leads/{leadId}/exam")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmEntranceExamDto> GetByLeadAsync([FromRoute] Guid leadId)
	{
		return await readService.GetByLeadAsync(leadId);
	}

	[HttpGet("exams/{examRecordId}/eligibility-handoff")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmEligibilityHandoffDto> GetEligibilityHandoffAsync(
		[FromRoute] Guid examRecordId)
	{
		return await readService.GetEligibilityHandoffAsync(examRecordId);
	}

	[HttpPost("leads/{leadId}/exam/schedule")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam)]
	public async Task<AdmissionCrmEntranceExamDto> ScheduleAsync(
		[FromRoute] Guid leadId,
		[FromBody] ScheduleAdmissionCrmEntranceExamDto input)
	{
		return await schedulingService.ScheduleAsync(leadId, input);
	}

	[HttpPost("exams/{examRecordId}/attendance")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public async Task<AdmissionCrmEntranceExamDto> MarkAttendanceAsync(
		[FromRoute] Guid examRecordId,
		[FromBody] MarkAdmissionCrmExamAttendanceDto input)
	{
		return await attendanceService.MarkAttendanceAsync(examRecordId, input);
	}

	[HttpPost("exams/{examRecordId}/result")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public async Task<AdmissionCrmEntranceExamDto> UploadResultAsync(
		[FromRoute] Guid examRecordId,
		[FromBody] UploadAdmissionCrmEntranceExamResultDto input)
	{
		return await resultService.UploadResultAsync(examRecordId, input);
	}

	[HttpPost("exams/{examRecordId}/eligibility")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public async Task<AdmissionCrmEntranceExamDto> DecideEligibilityAsync(
		[FromRoute] Guid examRecordId,
		[FromBody] UpdateAdmissionCrmEligibilityDecisionDto input)
	{
		return await eligibilityService.DecideEligibilityAsync(examRecordId, input);
	}
}
