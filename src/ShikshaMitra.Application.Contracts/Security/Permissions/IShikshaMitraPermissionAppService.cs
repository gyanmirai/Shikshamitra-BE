using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Security.Permissions;

public interface IShikshaMitraPermissionAppService : IApplicationService
{
	Task<GetNestedPermissionListResultDto> GetNestedAsync(string providerName, string providerKey);
}
