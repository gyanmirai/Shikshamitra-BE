using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.IssueReturn;

public interface ILibraryCopyHistoryAppService : IApplicationService
{
	Task<PaginatedResponse<IssueReturnItemDto>> GetAsync(Guid bookId, Guid copyId, int page = 1, int pageSize = 20,
		Guid? academicYearId = null, DateOnly? asOfDate = null);
}
