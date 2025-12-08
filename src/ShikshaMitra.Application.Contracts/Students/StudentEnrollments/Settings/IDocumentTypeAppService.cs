using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Application service interface for managing document types
/// </summary>
public interface IDocumentTypeAppService : IApplicationService
{
	/// <summary>
	/// Gets a paginated list of document types
	/// </summary>
	Task<PagedResultDto<DocumentTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input);

	/// <summary>
	/// Gets a document type by ID
	/// </summary>
	Task<DocumentTypeDto> GetAsync(Guid id);

	/// <summary>
	/// Creates a new document type
	/// </summary>
	Task<DocumentTypeDto> CreateAsync(CreateUpdateDocumentTypeDto input);

	/// <summary>
	/// Updates an existing document type
	/// </summary>
	Task<DocumentTypeDto> UpdateAsync(Guid id, CreateUpdateDocumentTypeDto input);

	/// <summary>
	/// Deletes a document type
	/// </summary>
	Task DeleteAsync(Guid id);
}
