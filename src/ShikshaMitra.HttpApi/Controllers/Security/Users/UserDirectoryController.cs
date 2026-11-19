using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Security.Users;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;

namespace ShikshaMitra.Controllers.Security.Users;

[Authorize]
[Authorize(IdentityPermissions.Users.Default)]
[Route("api/security/user-directory")]
public class UserDirectoryController(IUserDirectoryAppService directory) : AbpController
{
    [HttpGet]
    public Task<PagedResultDto<UserDirectoryDto>> GetListAsync([FromQuery] GetIdentityUsersInput input) =>
        directory.GetListAsync(input);
}
