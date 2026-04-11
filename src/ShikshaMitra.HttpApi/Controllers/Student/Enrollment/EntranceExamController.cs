using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.EntranceExams;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student entrance exam operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/entrance-exam")]
public class EntranceExamController(
	IStudentEnrollmentEntranceExamAppService entranceExamAppService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<Result<PagedResultDto<EntranceExamStudentDto>>> GetListAsync(
		[FromQuery] GetEntranceExamStudentsInput input)
	{
		var result = await entranceExamAppService.GetListAsync(input);
		return Result.Success(result);
	}

	[HttpGet("{studentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<Result<EntranceExamDetailDto>> GetAsync(Guid studentId)
	{
		var result = await entranceExamAppService.GetAsync(studentId);
		return Result.Success(result);
	}

	[HttpPost("{studentId}/schedule")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task<Result> ScheduleExamAsync(Guid studentId, [FromBody] ScheduleEntranceExamDto input)
	{
		await entranceExamAppService.ScheduleExamAsync(studentId, input);
		return Result.Success();
	}

	[HttpPost("{studentId}/result")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task<Result<EntranceExamResultResponseDto>> UploadResultAsync(
		Guid studentId, [FromBody] UploadEntranceExamResultDto input)
	{
		var result = await entranceExamAppService.UploadResultAsync(studentId, input);
		return Result.Success(result);
	}

	/// <summary>
	/// Moves a student to admission stage.
	/// Requires either Enrollment_EntranceExam.Add or Enrollment_RegistrationFormIssued.Add permission.
	/// The OR-logic is enforced inside the application service until a combined ABP policy is defined.
	/// </summary>
	[HttpPost("{studentId}/move-to-admission")]
	[Authorize]
	public async Task<Result> MoveToAdmissionAsync(Guid studentId, [FromQuery] Guid? academicYearId = null)
	{
		await entranceExamAppService.MoveToAdmissionAsync(studentId, academicYearId);
		return Result.Success();
	}

	[HttpPost("bulk-move-to-admission")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task<Result<BulkMoveResultDto>> BulkMoveToAdmissionAsync([FromBody] BulkMoveToAdmissionDto input)
	{
		var result = await entranceExamAppService.BulkMoveToAdmissionAsync(input);
		return Result.Success(result);
	}
}
