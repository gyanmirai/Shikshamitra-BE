using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.TenantSettings.DocumentTypes;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings;

/// <summary>
/// API controller for managing document types
/// </summary>
[ApiController]
[Authorize]
[Route("api/tenant-settings/document-types")]
public class DocumentTypeController(IDocumentTypeAppService documentTypeAppService) : AbpControllerBase
{
	/// <summary>
	/// Gets a paginated list of document types
	/// </summary>
	[HttpGet]
	public async Task<Result<PagedResultDto<DocumentTypeDto>>> GetList(
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10)
	{
		var result = await documentTypeAppService.GetListAsync(new PagedAndSortedResultRequestDto
		{
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});

		return Result.Success(result);
	}

	/// <summary>
	/// Gets a document type by ID
	/// </summary>
	[HttpGet("{id}")]
	public async Task<Result<DocumentTypeDto>> Get(Guid id)
	{
		var result = await documentTypeAppService.GetAsync(id);
		return Result.Success(result);
	}

	/// <summary>
	/// Creates a new document type
	/// </summary>
	[HttpPost]
	public async Task<Result<DocumentTypeDto>> Create(
		[FromBody] CreateUpdateDocumentTypeDto input)
	{
		var result = await documentTypeAppService.CreateAsync(input);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates an existing document type
	/// </summary>
	[HttpPut("{id}")]
	public async Task<Result<DocumentTypeDto>> Update(
		Guid id,
		[FromBody] CreateUpdateDocumentTypeDto input)
	{
		var result = await documentTypeAppService.UpdateAsync(id, input);
		return Result.Success(result);
	}

	/// <summary>
	/// Deletes a document type
	/// </summary>
	[HttpDelete("{id}")]
	public async Task<Result> Delete(Guid id)
	{
		await documentTypeAppService.DeleteAsync(id);
		return Result.Success();
	}
}
