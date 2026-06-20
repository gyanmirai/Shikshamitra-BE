using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;

/// <summary>
/// API controller for managing student admission operations
/// </summary>
[ApiController]
[Authorize]
[RemoteService(Name = "Students")]
[Area("students")]
[Route("api/students/admissions")]
[Route("api/student/enrollment/admissions")]
public class AdmissionController : AbpControllerBase
{
	private readonly IStudentAdmissionAppService _admissionAppService;

	public AdmissionController(IStudentAdmissionAppService admissionAppService)
	{
		_admissionAppService = admissionAppService;
	}

	/// <summary>
	/// Gets a paginated list of students in admission stage
	/// GET: api/students/admissions
	/// </summary>
	/// <param name="input">Filter and pagination parameters</param>
	/// <returns>Paginated list of students in admission stage</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.View)]
	public async Task<PagedResultDto<StudentDto>> GetListAsync([FromQuery] GetStudentAdmissionsInput input)
	{
		return await _admissionAppService.GetListAsync(input);
	}

	/// <summary>
	/// Assigns or changes division for a student
	/// PUT: api/students/admissions/{studentId}/division
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">Division assignment data</param>
	[HttpPut("{studentId}/division")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task AssignDivisionAsync(Guid studentId, [FromBody] AssignDivisionDto input)
	{
		await _admissionAppService.AssignDivisionAsync(studentId, input);
	}

	/// <summary>
	/// Updates admission-stage general details using the registration update schema.
	/// PUT: api/student/enrollment/admissions/{studentId}
	/// </summary>
	/// <param name="studentId">The student identifier</param>
	/// <param name="input">General information data</param>
	[HttpPut("{studentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	[ProducesResponseType(
		typeof(ShikshaMitra.Common.Responses.Result<ShikshaMitra.Students.Enrollments.Registrations.StudentGeneralDetailsDto>),
		Microsoft.AspNetCore.Http.StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ShikshaMitra.Common.Responses.Result), Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ShikshaMitra.Common.Responses.Result), Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound)]
	public async Task<ShikshaMitra.Common.Responses.Result<ShikshaMitra.Students.Enrollments.Registrations.StudentGeneralDetailsDto>>
		UpdateAdmissionGeneralDetailsAsync(
			Guid studentId,
			[FromBody] ShikshaMitra.Students.Enrollments.Registrations.UpdateGeneralInfoDto input)
	{
		var result = await _admissionAppService.UpdateAdmissionGeneralDetailsAsync(studentId, input);
		return ShikshaMitra.Common.Responses.Result.Success(result);
	}

	/// <summary>
	/// Changes admission status (approve/reject) for multiple students
	/// POST: api/students/admissions/status
	/// </summary>
	/// <param name="input">Batch status change request</param>
	/// <returns>Result with success/failure details for each student</returns>
	[HttpPost("status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_Admission.Add)]
	public async Task<BatchAdmissionStatusChangeResultDto> ChangeStatusBatchAsync([FromBody] BatchAdmissionStatusChangeRequestDto input)
	{
		return await _admissionAppService.ChangeStatusBatchAsync(input);
	}
}
