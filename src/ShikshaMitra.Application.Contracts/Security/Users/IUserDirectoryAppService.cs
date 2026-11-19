using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;

namespace ShikshaMitra.Security.Users;

public interface IUserDirectoryAppService : IApplicationService
{
    Task<PagedResultDto<UserDirectoryDto>> GetListAsync(GetIdentityUsersInput input);
}
