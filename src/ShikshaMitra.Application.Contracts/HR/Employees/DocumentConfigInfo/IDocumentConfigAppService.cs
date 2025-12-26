using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Employees.DocumentConfigInfo;

/// <summary>
/// Application service interface for managing document configurations
/// </summary>
public interface IDocumentConfigAppService
{
	Task<IEnumerable<DocumentConfigDto>> GetListAsync(int page, int pageSize);
	Task<DocumentConfigDto> GetAsync(Guid documentId);
	Task<Guid> CreateAsync(CreateDocumentConfigDto input);
	Task UpdateAsync(Guid documentId, UpdateDocumentConfigDto input);
	Task DeleteAsync(Guid documentId);
}
