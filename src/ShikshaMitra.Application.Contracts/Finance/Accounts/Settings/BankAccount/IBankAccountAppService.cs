using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Settings.BankAccount.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Accounts.Settings.BankAccount;

public interface IBankAccountAppService : IApplicationService
{
    Task<BankAccountResponse> CreateAsync(Guid academicYearId, CreateBankAccountRequest input);
    Task<PaginatedBankAccountResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
    Task<BankAccountResponse> GetAsync(Guid id);
    Task<BankAccountResponse> UpdateAsync(Guid id, UpdateBankAccountRequest input);
    Task DeleteAsync(Guid id);
}
