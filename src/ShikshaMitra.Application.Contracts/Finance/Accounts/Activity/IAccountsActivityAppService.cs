using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.Activity;

public interface IAccountsActivityAppService
{
    Task<AccountsActivityPageDto> GetListAsync(AccountsActivityInput input);
}
