using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.Activity;

public interface IAccountsActivityAppService
{
    Task<AccountsActivityPageDto> GetListAsync(AccountsActivityInput input);
    Task<AccountsActivityDetailDto> GetDetailAsync(Guid academicYearId, string source, Guid id);
}
