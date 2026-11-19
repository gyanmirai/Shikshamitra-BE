using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Settings;

public interface IBankAccountAppService : IApplicationService
{
	Task<BankAccountResponse> CreateAsync(Guid academicYearId, CreateBankAccountRequest input);
	Task<PaginatedBankAccountResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10,
		string? search = null, bool includeInactive = true, string? filters = null);
	Task<BankAccountResponse> GetAsync(Guid id);
	Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(Guid id, int skipCount = 0, int maxResultCount = 50);
	Task<BankAccountResponse> UpdateAsync(Guid id, UpdateBankAccountRequest input);
	Task<BankAccountResponse> UpdateStatusAsync(Guid id, AccountSettingStatusUpdateRequest input);
	Task DeleteAsync(Guid id);
}
