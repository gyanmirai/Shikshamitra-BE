using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Admissions;

/// <summary>
/// API controller for managing student admission operations
/// </summary>
[ApiController]
[Authorize]
[RemoteService(Name = "Students")]
[Area("students")]
[Route("api/students/admissions")]
public class StudentAdmissionController : AbpControllerBase
{
	private readonly IStudentAdmissionAppService _admissionAppService;

	public StudentAdmissionController(IStudentAdmissionAppService admissionAppService)
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
	public async Task<PagedResultDto<StudentAdmissionListItemDto>> GetListAsync([FromQuery] GetStudentAdmissionsInput input)
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
