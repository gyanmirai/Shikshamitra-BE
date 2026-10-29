using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;

namespace ShikshaMitra.Security.Users;

[RemoteService(false)]
[Authorize]
[Authorize(IdentityPermissions.Users.Default)]
public class UserDirectoryAppService(
    IIdentityUserAppService identityUsers,
    IUserScopeResolutionRepository memberships) : ShikshaMitraAppService, IUserDirectoryAppService
{
    public virtual async Task<PagedResultDto<UserDirectoryDto>> GetListAsync(GetIdentityUsersInput input)
    {
        await AuthorizationService.CheckAsync(IdentityPermissions.Users.Default);
        var page = await identityUsers.GetListAsync(input);
        var roles = await memberships.GetRoleNamesByUserIdsAsync(page.Items.Select(user => user.Id).ToArray());
        return new PagedResultDto<UserDirectoryDto>(page.TotalCount, page.Items.Select(user => new UserDirectoryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Name = user.Name,
            Surname = user.Surname,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleNames = roles.GetValueOrDefault(user.Id, []).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList()
        }).ToList());
    }
}
