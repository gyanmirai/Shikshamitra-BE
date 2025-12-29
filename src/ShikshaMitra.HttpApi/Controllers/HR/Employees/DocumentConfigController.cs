using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Document configuration management controller
/// Handles CRUD operations for employee document configurations (matches OpenAPI contract)
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees/documents")]
[Authorize]
public class DocumentConfigController : AbpControllerBase
{
	private readonly IDocumentSettingAppService _documentSettingAppService;

	public DocumentConfigController(IDocumentSettingAppService documentSettingAppService)
	{
		_documentSettingAppService = documentSettingAppService;
	}

	/// <summary>
	/// Get all document configurations (paginated)
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(DocumentListResponse), StatusCodes.Status200OK)]
	public virtual async Task<DocumentListResponse> GetListAsync(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10)
	{
		return await _documentSettingAppService.GetDocumentListAsync(page, pageSize);
	}

	/// <summary>
	/// Get document configuration by ID
	/// </summary>
	[HttpGet("{documentId}")]
	[ProducesResponseType(typeof(DocumentItem), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<DocumentItem> GetAsync([FromRoute, Required] Guid documentId)
	{
		return await _documentSettingAppService.GetDocumentByIdAsync(documentId);
	}

	/// <summary>
	/// Create document configuration
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(DocumentCreateResponse), StatusCodes.Status201Created)]
	public virtual async Task<ActionResult<DocumentCreateResponse>> CreateAsync(
		[FromBody, Required] DocumentRequest request)
	{
		var response = await _documentSettingAppService.CreateDocumentAsync(request);
		return StatusCode(StatusCodes.Status201Created, response);
	}

	/// <summary>
	/// Update document configuration
	/// </summary>
	[HttpPut("{documentId}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid documentId,
		[FromBody, Required] DocumentRequest request)
	{
		await _documentSettingAppService.UpdateDocumentAsync(documentId, request);
		return NoContent();
	}

	/// <summary>
	/// Delete document configuration
	/// </summary>
	[HttpDelete("{documentId}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public virtual async Task<IActionResult> DeleteAsync([FromRoute, Required] Guid documentId)
	{
		await _documentSettingAppService.DeleteDocumentAsync(documentId);
		return NoContent();
	}
}
