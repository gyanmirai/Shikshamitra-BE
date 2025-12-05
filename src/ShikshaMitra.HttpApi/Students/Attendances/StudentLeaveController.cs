using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// REST API Controller for Student Leave management
/// </summary>
[RemoteService(Name = "Students")]
[Area("students")]
[Route("api/students/attendance/leaves")]
[Authorize]
public class StudentLeaveController : AbpControllerBase, IStudentLeaveAppService
{
	private readonly IStudentLeaveAppService _studentLeaveAppService;
	
	public StudentLeaveController(IStudentLeaveAppService studentLeaveAppService)
	{
		_studentLeaveAppService = studentLeaveAppService;
	}
	
	/// <summary>
	/// Gets a paginated list of student leave requests
	/// GET: api/students/attendance/leaves
	/// </summary>
	/// <param name="input">Filter and pagination parameters</param>
	/// <returns>Paginated list of student leave requests</returns>
	[HttpGet]
	public Task<PagedResultDto<StudentLeaveDto>> GetListAsync([FromQuery] GetStudentLeaveListInput input)
	{
		return _studentLeaveAppService.GetListAsync(input);
	}
	
	/// <summary>
	/// Gets a student leave request by ID
	/// GET: api/students/attendance/leaves/{id}
	/// </summary>
	/// <param name="id">Leave request ID</param>
	/// <returns>Student leave details</returns>
	[HttpGet("{id}")]
	public Task<StudentLeaveDto> GetAsync(Guid id)
	{
		return _studentLeaveAppService.GetAsync(id);
	}
	
	/// <summary>
	/// Creates a new student leave request
	/// POST: api/students/attendance/leaves
	/// </summary>
	/// <param name="input">Leave request data</param>
	/// <returns>Created leave request</returns>
	[HttpPost]
	public Task<StudentLeaveDto> CreateAsync([FromBody] CreateStudentLeaveDto input)
	{
		return _studentLeaveAppService.CreateAsync(input);
	}
	
	/// <summary>
	/// Updates an existing student leave request (only if status is Pending)
	/// PUT: api/students/attendance/leaves/{id}
	/// </summary>
	/// <param name="id">Leave request ID</param>
	/// <param name="input">Updated leave request data</param>
	/// <returns>Updated leave request</returns>
	[HttpPut("{id}")]
	public Task<StudentLeaveDto> UpdateAsync(Guid id, [FromBody] UpdateStudentLeaveDto input)
	{
		return _studentLeaveAppService.UpdateAsync(id, input);
	}
	
	/// <summary>
	/// Deletes a student leave request
	/// DELETE: api/students/attendance/leaves/{id}
	/// </summary>
	/// <param name="id">Leave request ID</param>
	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return _studentLeaveAppService.DeleteAsync(id);
	}
	
	/// <summary>
	/// Approves a student leave request
	/// POST: api/students/attendance/leaves/{id}/approve
	/// </summary>
	/// <param name="id">Leave request ID</param>
	/// <param name="input">Approval data with remarks</param>
	/// <returns>Approved leave request</returns>
	[HttpPost("{id}/approve")]
	public Task<StudentLeaveDto> ApproveAsync(Guid id, [FromBody] ApproveRejectStudentLeaveDto input)
	{
		return _studentLeaveAppService.ApproveAsync(id, input);
	}
	
	/// <summary>
	/// Rejects a student leave request
	/// POST: api/students/attendance/leaves/{id}/reject
	/// </summary>
	/// <param name="id">Leave request ID</param>
	/// <param name="input">Rejection data with reason and remarks</param>
	/// <returns>Rejected leave request</returns>
	[HttpPost("{id}/reject")]
	public Task<StudentLeaveDto> RejectAsync(Guid id, [FromBody] ApproveRejectStudentLeaveDto input)
	{
		return _studentLeaveAppService.RejectAsync(id, input);
	}
	
	/// <summary>
	/// Downloads the attachment file for a leave request
	/// GET: api/students/attendance/leaves/{id}/attachment
	/// </summary>
	/// <param name="id">Leave request ID</param>
	/// <returns>File attachment</returns>
	[HttpGet("{id}/attachment")]
	public Task<StudentLeaveAttachmentDto> GetAttachmentAsync(Guid id)
	{
		return _studentLeaveAppService.GetAttachmentAsync(id);
	}
	
	/// <summary>
	/// Downloads the attachment file as a file download
	/// GET: api/students/attendance/leaves/{id}/attachment/download
	/// </summary>
	/// <param name="id">Leave request ID</param>
	/// <returns>File download</returns>
	[HttpGet("{id}/attachment/download")]
	public async Task<IActionResult> DownloadAttachmentAsync(Guid id)
	{
		var attachment = await _studentLeaveAppService.GetAttachmentAsync(id);
		return File(attachment.FileContent, attachment.ContentType, attachment.FileName);
	}
}
