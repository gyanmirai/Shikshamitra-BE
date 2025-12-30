using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.EntranceExams;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student entrance exam operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/entrance-exam")]
public class EntranceExamController : AbpControllerBase
{
	private readonly IStudentEnrollmentEntranceExamAppService entranceExamAppService;

	public EntranceExamController(IStudentEnrollmentEntranceExamAppService entranceExamAppService)
	{
		this.entranceExamAppService = entranceExamAppService;
	}

	/// <summary>
	/// Gets a paginated list of students in entrance exam stage
	/// </summary>
	/// <param name="input">Filter and pagination parameters</param>
	/// <returns>Paginated list of students in entrance exam stage</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<PagedResultDto<EntranceExamStudentDto>> GetListAsync([FromQuery] GetEntranceExamStudentsInput input)
	{
		return await entranceExamAppService.GetListAsync(input);
	}

	/// <summary>
	/// Gets detailed entrance exam information for a specific student
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <returns>Entrance exam details for the student</returns>
	[HttpGet("{studentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<EntranceExamDetailDto> GetAsync(Guid studentId)
	{
		return await entranceExamAppService.GetAsync(studentId);
	}

	/// <summary>
	/// Schedules a student for an entrance exam slot
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Exam scheduling data</param>
	[HttpPost("{studentId}/schedule")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task ScheduleExamAsync(Guid studentId, [FromBody] ScheduleEntranceExamDto input)
	{
		await entranceExamAppService.ScheduleExamAsync(studentId, input);
	}

	/// <summary>
	/// Uploads entrance exam result for a student
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Exam result data</param>
	/// <returns>Result summary with status</returns>
	[HttpPost("{studentId}/result")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task<EntranceExamResultResponseDto> UploadResultAsync(Guid studentId, [FromBody] UploadEntranceExamResultDto input)
	{
		return await entranceExamAppService.UploadResultAsync(studentId, input);
	}

	/// <summary>
	/// Moves a student who passed the entrance exam to admission stage
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	[HttpPost("{studentId}/move-to-admission")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add + "," + ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task MoveToAdmissionAsync(Guid studentId)
	{
		await entranceExamAppService.MoveToAdmissionAsync(studentId);
	}

	/// <summary>
	/// Moves multiple students to admission stage in bulk
	/// </summary>
	/// <param name="input">List of student IDs to move</param>
	/// <returns>Bulk operation result with success and failure details</returns>
	[HttpPost("bulk-move-to-admission")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task<BulkMoveResultDto> BulkMoveToAdmissionAsync([FromBody] BulkMoveToAdmissionDto input)
	{
		return await entranceExamAppService.BulkMoveToAdmissionAsync(input);
	}
}
