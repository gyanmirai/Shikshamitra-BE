using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records/list")]
public class StudentListController(
	IStudentListAppService studentListAppService
) : AbpController
{
	// get students by division is handled by StudentController.GetStudentsByDivisionAsync to keep pagination and search concerns together.
	[HttpGet("{divisionId}")]
	public async Task<PagedResultDto<StudentDto>> GetStudentsByDivision(Guid divisionId, [FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		return await studentListAppService.GetStudentsByDivisionAsync(divisionId, skipCount, maxResultCount, searchTerm);
	}

	/// <summary>
	/// Changes the operational status (Active / Inactive / Left) of an enrolled student.
	/// A status-change history entry with the provided remarks is automatically persisted.
	/// </summary>
	[HttpPut("{studentId}/status")]
	public async Task ChangeStudentStatus(Guid studentId, [FromBody] ChangeStudentStatusDto input)
	{
		await studentListAppService.ChangeStudentStatusAsync(studentId, input);
	}

	/// <summary>
	/// Returns the full audited status-change history for a student, newest entry first.
	/// </summary>
	[HttpGet("{studentId}/status/history")]
	public async Task<List<StudentStatusHistoryDto>> GetStatusHistory(Guid studentId)
	{
		return await studentListAppService.GetStatusHistoryAsync(studentId);
	}
}
