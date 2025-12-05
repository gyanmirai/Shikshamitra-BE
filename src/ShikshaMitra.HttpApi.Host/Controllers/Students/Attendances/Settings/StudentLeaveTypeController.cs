using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.Attendances.Settings;

/// <summary>
/// REST API Controller for Student Leave Type management
/// </summary>
[RemoteService(Name = "Students")]
[Area("students")]
[Route("api/students/attendance/settings/leave-types")]
public class StudentLeaveTypeController : AbpControllerBase, IStudentLeaveTypeAppService
{
	private readonly IStudentLeaveTypeAppService _studentLeaveTypeAppService;

	public StudentLeaveTypeController(IStudentLeaveTypeAppService studentLeaveTypeAppService)
	{
		_studentLeaveTypeAppService = studentLeaveTypeAppService;
	}

	/// <summary>
	/// Gets a paginated list of student leave types
	/// GET: api/students/attendance/settings/leave-types
	/// </summary>
	[HttpGet]
	public Task<PagedResultDto<StudentLeaveTypeDto>> GetListAsync([FromQuery] GetStudentLeaveTypeListInput input)
	{
		return _studentLeaveTypeAppService.GetListAsync(input);
	}

	/// <summary>
	/// Gets a student leave type by ID
	/// GET: api/students/attendance/settings/leave-types/{id}
	/// </summary>
	[HttpGet("{id}")]
	public Task<StudentLeaveTypeDto> GetAsync(Guid id)
	{
		return _studentLeaveTypeAppService.GetAsync(id);
	}

	/// <summary>
	/// Creates a new student leave type
	/// POST: api/students/attendance/settings/leave-types
	/// </summary>
	[HttpPost]
	public Task<StudentLeaveTypeDto> CreateAsync([FromBody] CreateUpdateStudentLeaveTypeDto input)
	{
		return _studentLeaveTypeAppService.CreateAsync(input);
	}

	/// <summary>
	/// Updates an existing student leave type
	/// PUT: api/students/attendance/settings/leave-types/{id}
	/// </summary>
	[HttpPut("{id}")]
	public Task<StudentLeaveTypeDto> UpdateAsync(Guid id, [FromBody] CreateUpdateStudentLeaveTypeDto input)
	{
		return _studentLeaveTypeAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Deletes a student leave type
	/// DELETE: api/students/attendance/settings/leave-types/{id}
	/// </summary>
	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return _studentLeaveTypeAppService.DeleteAsync(id);
	}

	/// <summary>
	/// Gets all active student leave types (for dropdowns)
	/// GET: api/students/attendance/settings/leave-types/active
	/// </summary>
	[HttpGet("active")]
	public Task<ListResultDto<StudentLeaveTypeDto>> GetActiveListAsync()
	{
		return _studentLeaveTypeAppService.GetActiveListAsync();
	}
}
