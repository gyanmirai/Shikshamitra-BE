using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Charges.Editor;

public interface ILibraryChargeEditorLookupAppService : IApplicationService
{
	Task<PaginatedResponse<LibraryChargeEditorCategoryDto>> GetCategoriesAsync(Guid academicYearId,
		int page = 1, int pageSize = 20, string? search = null);
	Task<PaginatedResponse<LibraryChargeEditorMemberDto>> SearchMembersAsync(Guid academicYearId,
		string? search = null, int page = 1, int pageSize = 20);
	Task<LibraryChargeEditorMemberDto> ResolveMemberAsync(Guid academicYearId, string identifier);
	Task<PaginatedResponse<LibraryChargeEditorLoanSourceDto>> GetLoanSourcesAsync(Guid academicYearId,
		Guid memberId, int page = 1, int pageSize = 20, string? search = null);
	Task<LibraryChargeEditorContextDto> GetUpdateContextAsync(Guid chargeId);
}
