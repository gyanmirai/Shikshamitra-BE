using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.Security.Permissions;

public partial class ShikshaMitraPermissionAppService
{
	private Task<bool> IsProtectedAdminAsync(string providerName, string providerKey) =>
		providerName == "R"
			? Task.FromResult(string.Equals(providerKey, AdminPermissionAccess.RoleName, StringComparison.OrdinalIgnoreCase))
			: _services.GetRequiredService<AdminPermissionAccess>().IsAdminAsync(Guid.Parse(providerKey));

	private async Task CheckAdminPermissionsEditableAsync(string providerName, string providerKey)
	{
		if (await IsProtectedAdminAsync(providerName, providerKey))
			throw new BusinessException(SecurityPermissionErrorCodes.AdminPermissionsReadOnly,
				"Admin has all permissions by default. Admin permissions cannot be changed.");
	}
}
