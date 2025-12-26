using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.Records;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records")]
public class StudentManagementController(
	IStudentManagementAppService studentManagementAppService
) : AbpController
{
	[HttpGet("{divisionId}")]
	public async Task<Result<List<StudentRecordDto>>> GetStudentRecords(Guid divisionId)
	{
		var studentRecords = await studentManagementAppService.GetStudentRecords(divisionId);
		return Result.Success(studentRecords);
	}

	[HttpPost("{studentId}/houses")]
	public async Task<Result> AssignHouseToStudent(Guid studentId, [FromQuery] Guid houseId)
	{
		await studentManagementAppService.AssignHouseToStudent(studentId, houseId);
		return Result.Success();
	}

	[HttpPost("{studentId}/house-global-leader")]
	public async Task<Result> AssignHouseGlobalLeader(Guid studentId)
	{
		await studentManagementAppService.AssignHouseGlobalLeaderAsync(studentId);
		return Result.Success();
	}

	[HttpPost("{studentId}/house-division-leader")]
	public async Task<Result> AssignHouseDivisionLeader(Guid studentId)
	{
		await studentManagementAppService.AssignHouseDivisionLeaderAsync(studentId);
		return Result.Success();
	}

	// API to deassign house from student
	[HttpDelete("{studentId}/houses")]
	public async Task<Result> DeassignHouseFromStudent(Guid studentId)
	{
		await studentManagementAppService.DeassignHouseFromStudent(studentId);
		return Result.Success();
	}

	// API to deassign house global leader from student
	[HttpDelete("{studentId}/house-global-leader")]
	public async Task<Result> DeassignHouseGlobalLeader(Guid studentId)
	{
		await studentManagementAppService.DeassignHouseGlobalLeaderAsync(studentId);
		return Result.Success();
	}

	// API to deassign house division leader from student
	[HttpDelete("{studentId}/house-division-leader")]
	public async Task<Result> DeassignHouseDivisionLeader(Guid studentId)
	{
		await studentManagementAppService.DeassignHouseDivisionLeaderAsync(studentId);
		return Result.Success();
	}


}
