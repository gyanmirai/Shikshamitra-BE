using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.IssueReturn;

public interface ILibraryCirculationLookupAppService : IApplicationService
{
	Task<LibraryCopyLookupDto> GetCopyAsync(string scan);
	Task<LibraryReturnLookupDto> GetReturnAsync(string scan);
	Task<PaginatedResponse<LibraryReturnAssessmentCategoryDto>> GetAssessmentCategoriesAsync(Guid academicYearId, int page = 1, int pageSize = 20, string? search = null);
}
