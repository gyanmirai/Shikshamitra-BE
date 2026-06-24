using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
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
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.View)]
	public async Task<Result<StudentRecordListResponseDto>> GetStudentRecords(Guid divisionId)
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

		return Result.Success(new StudentRecordListResponseDto
		{
			TotalCount = studentRecords.Count,
			CurrentPage = 1,
			PageSize = studentRecords.Count,
			TotalPages = studentRecords.Count == 0 ? 0 : 1,
			ArrangementType = arrangementType,
			ArrangementBy = arrangementBy,
			Items = studentRecords
		});
	}

	[HttpGet("pending-assignments")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.View)]
	public async Task<Result<PendingAssignmentResponseDto>> GetPendingAssignments([FromQuery] GetPendingAssignmentStudentsInput input)
	{
		var result = await studentManagementAppService.GetPendingAssignmentsAsync(input);
		return Result.Success(result);
	}

	[HttpPost("{studentId}/houses")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result> AssignHouseToStudent(Guid studentId, [FromQuery] Guid houseId)
	{
		await studentManagementAppService.AssignHouseToStudent(studentId, houseId);
		return Result.Success();
	}

	[HttpPost("{studentId}/house-global-leader")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result> AssignHouseGlobalLeader(Guid studentId)
	{
		await studentManagementAppService.AssignHouseGlobalLeaderAsync(studentId);
		return Result.Success();
	}

	[HttpPost("{studentId}/house-division-leader")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result> AssignHouseDivisionLeader(Guid studentId)
	{
		await studentManagementAppService.AssignHouseDivisionLeaderAsync(studentId);
		return Result.Success();
	}

	// API to deassign house from student
	[HttpDelete("{studentId}/houses")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Delete)]
	public async Task<Result> DeassignHouseFromStudent(Guid studentId)
	{
		await studentManagementAppService.DeassignHouseFromStudent(studentId);
		return Result.Success();
	}

	// API to deassign house global leader from student
	[HttpDelete("{studentId}/house-global-leader")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Delete)]
	public async Task<Result> DeassignHouseGlobalLeader(Guid studentId)
	{
		await studentManagementAppService.DeassignHouseGlobalLeaderAsync(studentId);
		return Result.Success();
	}

	// API to deassign house division leader from student
	[HttpDelete("{studentId}/house-division-leader")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Delete)]
	public async Task<Result> DeassignHouseDivisionLeader(Guid studentId)
	{
		await studentManagementAppService.DeassignHouseDivisionLeaderAsync(studentId);
		return Result.Success();
	}

	// API to get projections of GR numbers for all students in division
	[HttpGet("{divisionId}/gr-projections")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.View)]
	public async Task<Result<List<GrProjectionDto>>> GetGrProjections(Guid divisionId)
	{
		var grProjections = await studentManagementAppService.GetGrProjections(divisionId);
		return Result.Success(grProjections);
	}

	// API to store GR numbers of students
	[HttpPost("gr-numbers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result> StoreGrNumbers([FromBody] List<UpdateStudentGrNumberDto> updateStudentGrNumberDto)
	{
		await studentManagementAppService.StoreGrNumbers(updateStudentGrNumberDto);
		return Result.Success();
	}

	[HttpGet("academic-terms")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.View)]
	public async Task<Result<List<AcademicTermDto>>> GetAcademicTerms([FromQuery] Guid academicYearId)
	{
		var terms = await studentManagementAppService.GetAcademicTermsAsync(academicYearId);
		return Result.Success(terms);
	}

	[HttpPost("academic-terms")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result<AcademicTermDto>> CreateAcademicTerm([FromBody] CreateAcademicTermDto input)
	{
		var term = await studentManagementAppService.CreateAcademicTermAsync(input);
		return Result.Success(term);
	}

	[HttpPost("academic-terms/{academicTermId}/current")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result<AcademicTermDto>> SetCurrentAcademicTerm(Guid academicTermId)
	{
		var term = await studentManagementAppService.SetCurrentAcademicTermAsync(academicTermId);
		return Result.Success(term);
	}

	// API to get roll number projections for all students in division
	[HttpGet("{divisionId}/roll-number-projections")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.View)]
	public async Task<Result<RollNumberProjectionResponseDto>> GetRollNumberProjections(Guid divisionId, [FromQuery] Guid? academicTermId, [FromQuery] ArrangementTypeEnum? arrangementType, [FromQuery] ArrangementByEnum? arrangementBy)
	{
		var rollNumberProjections = await studentManagementAppService.GetRollNumberProjections(divisionId, academicTermId, arrangementType, arrangementBy);
		return Result.Success(rollNumberProjections);
	}

	// API to store roll number projections of students
	[HttpPost("{divisionId}/roll-numbers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result<RollNumberSaveResultDto>> StoreRollNumbers(Guid divisionId, [FromBody] List<UpdateStudentRollNumberDto> updateStudentRollNumberDto, [FromQuery] Guid? academicTermId, [FromQuery] ArrangementTypeEnum? arrangementType, [FromQuery] ArrangementByEnum? arrangementBy)
	{
		var result = await studentManagementAppService.StoreRollNumbers(divisionId, academicTermId, updateStudentRollNumberDto, arrangementType, arrangementBy);
		return Result.Success(result);
	}

	// API to change division of student
	[HttpPost("divisions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	public async Task<Result> ChangeDivision([FromBody] List<ChangeStudentDivisionDto> changeStudentDivisionDto)
	{
		await studentManagementAppService.ChangeDivision(changeStudentDivisionDto);
		return Result.Success();
	}
}
