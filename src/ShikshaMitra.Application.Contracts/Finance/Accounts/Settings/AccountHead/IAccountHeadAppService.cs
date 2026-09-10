using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Settings;

public interface IAccountHeadAppService : IApplicationService
{
	Task<AccountHeadResponse> CreateAsync(Guid academicYearId, AccountHeadUpsertRequest input);
	Task<PagedResultDto<AccountHeadResponse>> GetListAsync(Guid academicYearId, AccountHeadType? headType = null,
		int page = 1, int pageSize = 10, string? search = null, bool includeInactive = true);
	Task<AccountHeadResponse> GetAsync(Guid id);
	Task<List<AccountHeadOptionDto>> GetOptionsAsync(Guid academicYearId, AccountHeadType? headType = null);
	Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(Guid id, int skipCount = 0, int maxResultCount = 50);
	Task<AccountHeadResponse> UpdateAsync(Guid id, AccountHeadUpsertRequest input);
	Task<AccountHeadResponse> UpdateStatusAsync(Guid id, AccountSettingStatusUpdateRequest input);
	Task DeleteAsync(Guid id);
}
