using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.DocumentConfigInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Document configuration management controller
/// Handles CRUD operations for employee document configurations
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees/documents")]
[Authorize]
public class DocumentConfigController : AbpControllerBase
{
	private readonly IDocumentConfigAppService _documentConfigAppService;

	public DocumentConfigController(IDocumentConfigAppService documentConfigAppService)
	{
		_documentConfigAppService = documentConfigAppService;
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
		var result = await _documentConfigAppService.GetListAsync(page, pageSize);
		return DocumentConfigContractMapper.ToListResponse(result);
	}

	/// <summary>
	/// Get document configuration by ID
	/// </summary>
	[HttpGet("{documentId}")]
	[ProducesResponseType(typeof(DocumentItemResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<DocumentItemResponse> GetAsync([FromRoute, Required] Guid documentId)
	{
		var document = await _documentConfigAppService.GetAsync(documentId);
		return DocumentConfigContractMapper.ToItemResponse(document);
	}

	/// <summary>
	/// Create document configuration
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(DocumentCreateResponse), StatusCodes.Status201Created)]
	public virtual async Task<ActionResult<DocumentCreateResponse>> CreateAsync(
		[FromBody, Required] DocumentRequest request)
	{
		var createDto = DocumentConfigContractMapper.ToCreateDto(request);
		var id = await _documentConfigAppService.CreateAsync(createDto);
		return StatusCode(StatusCodes.Status201Created, new DocumentCreateResponse { Id = id });
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
		var updateDto = DocumentConfigContractMapper.ToUpdateDto(request);
		await _documentConfigAppService.UpdateAsync(documentId, updateDto);
		return NoContent();
	}

	/// <summary>
	/// Delete document configuration
	/// </summary>
	[HttpDelete("{documentId}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public virtual async Task<IActionResult> DeleteAsync([FromRoute, Required] Guid documentId)
	{
		await _documentConfigAppService.DeleteAsync(documentId);
		return NoContent();
	}
}
