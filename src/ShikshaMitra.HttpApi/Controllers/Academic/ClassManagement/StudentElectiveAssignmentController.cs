using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement;
using ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[Route("api/class-management/student-elective-assignments")]
[Authorize]
[ApiController]
public class StudentElectiveAssignmentController(
	IStudentElectiveAssignmentAppService appService
) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.View)]
	[ProducesResponseType(typeof(PaginatedStudentElectiveResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	[ProducesResponseType(404)]
	public async Task<IActionResult> GetStudentElectiveAssignmentsAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] Guid classId,
		[FromQuery] Guid divisionId,
		[FromQuery] Guid electiveGroupId,
		[FromQuery] string? search = null
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return BadRequest(new { error = "academicMediumId is required" });
		}

		if (classId == Guid.Empty)
		{
			return BadRequest(new { error = "classId is required" });
		}

		if (divisionId == Guid.Empty)
		{
			return BadRequest(new { error = "divisionId is required" });
		}

		if (electiveGroupId == Guid.Empty)
		{
			return BadRequest(new { error = "electiveGroupId is required" });
		}

		var result = await appService.GetStudentElectiveAssignmentsAsync(
			academicMediumId,
			classId,
			divisionId,
			electiveGroupId,
			search
		);

		return Ok(result);
	}

	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public async Task<IActionResult> SaveStudentElectiveAssignmentsAsync(
		[FromBody] SaveStudentElectiveAssignmentsDto input
	)
	{
		var result = await appService.SaveStudentElectiveAssignmentsAsync(input);
		return Ok(result);
	}

	[HttpPost("lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add)]
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(403)]
	public async Task<IActionResult> LockStudentElectiveAssignmentsAsync(
		[FromBody] LockStudentElectiveAssignmentsDto input
	)
	{
		var result = await appService.LockStudentElectiveAssignmentsAsync(input);
		return Ok(result);
	}
}
