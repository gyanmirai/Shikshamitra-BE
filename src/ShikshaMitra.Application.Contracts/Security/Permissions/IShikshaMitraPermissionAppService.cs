using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Security.Permissions;

public interface IShikshaMitraPermissionAppService : IApplicationService
{
	Task<UserPermissionOverridesDto> GetUserOverridesAsync(Guid userId);
	Task UpdateUserOverridesAsync(Guid userId, UpdateUserPermissionOverridesDto input);
	Task<GetNestedPermissionListResultDto> GetNestedAsync(string providerName, string providerKey);
}
