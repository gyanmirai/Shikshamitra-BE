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
		var (studentRecords, arrangementType, arrangementBy) = await studentManagementAppService.GetStudentRecords(divisionId);
		
		if (arrangementType.HasValue)
		{
			Response.Headers["X-Arrangement-Type"] = arrangementType.Value.ToString();
		}
		
		if (arrangementBy.HasValue)
		{
			Response.Headers["X-Arrangement-By"] = arrangementBy.Value.ToString();
		}
		
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

	// API to get projections of GR numbers for all students in division
	[HttpGet("{divisionId}/gr-projections")]
	public async Task<Result<List<GrProjectionDto>>> GetGrProjections(Guid divisionId)
	{
		var grProjections = await studentManagementAppService.GetGrProjections(divisionId);
		return Result.Success(grProjections);
	}

	// API to store GR numbers of students
	[HttpPost("gr-numbers")]
	public async Task<Result> StoreGrNumbers([FromBody] List<UpdateStudentGrNumberDto> updateStudentGrNumberDto)
	{
		await studentManagementAppService.StoreGrNumbers(updateStudentGrNumberDto);
		return Result.Success();
	}

	// API to get roll number projections for all students in division
	[HttpGet("{divisionId}/roll-number-projections")]
	public async Task<Result<List<RollNumberProjectionDto>>> GetRollNumberProjections(Guid divisionId, [FromQuery] ArrangementTypeEnum? arrangementType, [FromQuery] ArrangementByEnum? arrangementBy)
	{
		var rollNumberProjections = await studentManagementAppService.GetRollNumberProjections(divisionId, arrangementType ?? ArrangementTypeEnum.Mix, arrangementBy ?? ArrangementByEnum.FirstName);
		return Result.Success(rollNumberProjections);
	}

	// API to store roll number projections of students
	[HttpPost("{divisionId}/roll-numbers")]
	public async Task<Result> StoreRollNumbers(Guid divisionId, [FromBody] List<UpdateStudentRollNumberDto> updateStudentRollNumberDto, [FromQuery] ArrangementTypeEnum? arrangementType, [FromQuery] ArrangementByEnum? arrangementBy)
	{
		await studentManagementAppService.StoreRollNumbers(divisionId, updateStudentRollNumberDto, arrangementType, arrangementBy);
		return Result.Success();
	}

	// API to change division of student
	[HttpPost("divisions")]
	public async Task<Result> ChangeDivision([FromBody] List<ChangeStudentDivisionDto> changeStudentDivisionDto)
	{
		await studentManagementAppService.ChangeDivision(changeStudentDivisionDto);
		return Result.Success();
	}
}
