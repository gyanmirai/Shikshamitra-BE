using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Settings;

public interface
	IDocumentSettingAppService : ICrudAppService<DocumentSettingDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateDocumentSettingDto>
{
	/// <summary>
	/// Get all document configurations (matches OpenAPI contract)
	/// </summary>
	Task<DocumentListResponse> GetDocumentListAsync(int page, int pageSize);
	
	/// <summary>
	/// Get document configuration by ID (matches OpenAPI contract)
	/// </summary>
	Task<DocumentItem> GetDocumentByIdAsync(Guid documentId);
	
	/// <summary>
	/// Create document configuration (matches OpenAPI contract)
	/// </summary>
	Task<DocumentCreateResponse> CreateDocumentAsync(DocumentRequest input);
	
	/// <summary>
	/// Update document configuration (matches OpenAPI contract)
	/// </summary>
	Task UpdateDocumentAsync(Guid documentId, DocumentRequest input);
	
	/// <summary>
	/// Delete document configuration (matches OpenAPI contract)
	/// </summary>
	Task DeleteDocumentAsync(Guid documentId);
}
