using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.Settings;

/// <summary>
/// API controller for managing document types
/// </summary>
[ApiController]
[Authorize]
[Route("api/student-enrollment/settings/document-types")]
public class DocumentTypeController(IDocumentTypeAppService documentTypeAppService) : AbpControllerBase
{
	/// <summary>
	/// Gets a paginated list of document types
	/// </summary>
	/// <param name="input">Pagination parameters</param>
	/// <returns>Paginated list of document types</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsDocumentTypes.View)]
	public async Task<PagedResultDto<DocumentTypeDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return await documentTypeAppService.GetListAsync(input);
	}

	/// <summary>
	/// Gets a specific document type by ID
	/// </summary>
	/// <param name="id">The document type identifier</param>
	/// <returns>Document type details</returns>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsDocumentTypes.View)]
	public async Task<DocumentTypeDto> GetAsync(Guid id)
	{
		return await documentTypeAppService.GetAsync(id);
	}

	/// <summary>
	/// Creates a new document type
	/// </summary>
	/// <param name="input">Document type creation data</param>
	/// <returns>Created document type</returns>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsDocumentTypes.Add)]
	public async Task<DocumentTypeDto> CreateAsync([FromBody] CreateUpdateDocumentTypeDto input)
	{
		return await documentTypeAppService.CreateAsync(input);
	}

	/// <summary>
	/// Updates an existing document type
	/// </summary>
	/// <param name="id">The document type identifier</param>
	/// <param name="input">Updated document type data</param>
	/// <returns>Updated document type</returns>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsDocumentTypes.Add)]
	public async Task<DocumentTypeDto> UpdateAsync(Guid id, [FromBody] CreateUpdateDocumentTypeDto input)
	{
		return await documentTypeAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Deletes a document type
	/// </summary>
	/// <param name="id">The document type identifier</param>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_SettingsDocumentTypes.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		await documentTypeAppService.DeleteAsync(id);
	}
}
