using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.BorrowingRules.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.BorrowingRules;

public interface IBorrowingRuleAppService : IApplicationService
{
	Task<PaginatedResponse<BorrowingRuleDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10);
	Task<GenericUpdatedResponse> UpdateRulesAsync(Guid academicYearId, BorrowingRulesUpdateRequest input);
}
