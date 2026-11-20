using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.IssueReturn;

public interface ILibraryCirculationReadAppService : IApplicationService
{
	Task<PaginatedResponse<IssueReturnItemDto>> GetListAsync(int page = 1, int pageSize = 20,
		Guid? academicYearId = null, DateOnly? asOfDate = null);
}
