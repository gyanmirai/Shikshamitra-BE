using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Settings;

public interface ITransactionCategoryAppService : IApplicationService
{
	Task<System.Collections.Generic.List<AccountHeadOptionDto>> GetAccountHeadOptionsAsync(Guid academicYearId, TransactionType categoryType);
    Task<TransactionCategoryResponse> CreateAsync(Guid academicYearId, CreateTransactionCategoryRequest input, TransactionType categoryType);
	Task<PaginatedTransactionCategoryResponse> GetListAsync(Guid academicYearId, TransactionType categoryType,
		int page = 1, int pageSize = 10, string? search = null, bool includeInactive = true);
    Task<TransactionCategoryResponse> GetAsync(Guid id, TransactionType categoryType);
    Task<TransactionCategoryResponse> UpdateAsync(Guid id, UpdateTransactionCategoryRequest input, TransactionType categoryType);
	Task<TransactionCategoryResponse> UpdateStatusAsync(Guid id, AccountSettingStatusUpdateRequest input, TransactionType categoryType);
	Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(Guid id, TransactionType categoryType, int skipCount = 0, int maxResultCount = 50);
    Task DeleteAsync(Guid id, TransactionType categoryType);
}
