using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement;
using ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[Route("api/electives/assignments")]
[Authorize]
[ApiController]
public class StudentElectiveAssignmentController(
	IStudentElectiveAssignmentAppService appService
) : AbpController
{
	[HttpGet]
	[ProducesResponseType(typeof(PaginatedStudentElectiveResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
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
	[ProducesResponseType(typeof(ApiSuccessResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<IActionResult> SaveStudentElectiveAssignmentsAsync(
		[FromBody] SaveStudentElectiveAssignmentsDto input
	)
	{
		var result = await appService.SaveStudentElectiveAssignmentsAsync(input);
		return Ok(result);
	}
}
