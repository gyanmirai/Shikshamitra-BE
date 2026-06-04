using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

public partial class LeaveManagementController
{
	/// <summary>
	/// Download leave attachment metadata (JSON). Use /attachment/download for raw file bytes.
	/// </summary>
	[HttpGet("{id:guid}/attachment")]
	[ProducesResponseType(typeof(LeaveAttachmentDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveAttachmentDto> GetAttachmentAsync([FromRoute, Required] Guid id)
	{
		return await _leaveManagementAppService.GetAttachmentAsync(id);
	}

	/// <summary>
	/// Download own leave attachment metadata (JSON).
	/// </summary>
	[HttpGet("my/{id:guid}/attachment")]
	[ProducesResponseType(typeof(LeaveAttachmentDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveAttachmentDto> GetMyAttachmentAsync([FromRoute, Required] Guid id)
	{
		return await _leaveManagementAppService.GetMyAttachmentAsync(id);
	}

	/// <summary>
	/// Download leave attachment as a file
	/// </summary>
	[HttpGet("{id:guid}/attachment/download")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> DownloadAttachmentAsync([FromRoute, Required] Guid id)
	{
		var attachment = await _leaveManagementAppService.GetAttachmentAsync(id);
		return File(attachment.FileContent, attachment.ContentType, attachment.FileName);
	}

	/// <summary>
	/// Download own leave attachment as a file.
	/// </summary>
	[HttpGet("my/{id:guid}/attachment/download")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> DownloadMyAttachmentAsync([FromRoute, Required] Guid id)
	{
		var attachment = await _leaveManagementAppService.GetMyAttachmentAsync(id);
		return File(attachment.FileContent, attachment.ContentType, attachment.FileName);
	}
}
