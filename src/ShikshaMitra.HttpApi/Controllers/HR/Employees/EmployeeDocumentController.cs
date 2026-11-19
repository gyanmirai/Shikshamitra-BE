using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Documents;
using ShikshaMitra.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees/{employeeId:guid}/documents")]
[Authorize]
public class EmployeeDocumentController(
	IEmployeeDocumentAppService documentAppService,
	IEmployeeAuthorizationService authorizationService)
	: AbpControllerBase
{
	[HttpPost]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UploadAsync(
		[FromRoute, Required] Guid employeeId,
		[FromForm] EmployeeDocumentUploadRequest request)
	{
		await authorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		if (request.File == null || request.File.Length == 0)
			throw new BusinessException(HRErrorCodes.EmployeeDocumentFileRequired);

		await using var memoryStream = new MemoryStream();
		await request.File.CopyToAsync(memoryStream);

		var fileName = request.FileName ?? request.File.FileName;

		await documentAppService.UploadDocumentAsync(
			employeeId,
			request.DocumentNameId,
			fileName,
			request.File.ContentType,
			memoryStream.ToArray(),
			request.File.Length);

		return StatusCode(StatusCodes.Status201Created);
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	[ProducesResponseType(typeof(EmployeeDocumentListResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<EmployeeDocumentListResponse> GetListAsync(
		[FromRoute, Required] Guid employeeId,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 500)] int pageSize = 10,
		[FromQuery] string? search = null)
	{
		await authorizationService.CheckEmployeeAccessAsync(employeeId);
		return await documentAppService.GetDocumentListAsync(employeeId, page, pageSize, search);
	}

	[HttpGet("checklist")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public Task<Volo.Abp.Application.Dtos.PagedResultDto<EmployeeDocumentChecklistItemDto>> GetChecklistAsync(Guid employeeId, [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, [FromQuery] string? search = null) =>
		documentAppService.GetChecklistAsync(employeeId, page, pageSize, search);

	[HttpGet("{documentId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DownloadAsync(
		[FromRoute, Required] Guid employeeId,
		[FromRoute, Required] Guid documentId)
	{
		await authorizationService.CheckEmployeeAccessAsync(employeeId);

		var document = await documentAppService.GetDocumentAsync(employeeId, documentId);
		return File(document.Content, document.ContentType, document.FileName);
	}

	[HttpPut("{documentId:guid}")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromRoute, Required] Guid documentId,
		[FromForm] EmployeeDocumentUploadRequest request)
	{
		await authorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		if (request.File == null || request.File.Length == 0)
			throw new BusinessException(HRErrorCodes.EmployeeDocumentFileRequired);

		await using var memoryStream = new MemoryStream();
		await request.File.CopyToAsync(memoryStream);

		var fileName = request.FileName ?? request.File.FileName;

		await documentAppService.UpdateDocumentAsync(
			employeeId,
			documentId,
			request.DocumentNameId,
			fileName,
			request.File.ContentType,
			memoryStream.ToArray(),
			request.File.Length);

		return Ok();
	}

	[HttpDelete("{documentId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Delete)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsync(
		[FromRoute, Required] Guid employeeId,
		[FromRoute, Required] Guid documentId)
	{
		await authorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		await documentAppService.DeleteDocumentAsync(employeeId, documentId);
		return NoContent();
	}
}
