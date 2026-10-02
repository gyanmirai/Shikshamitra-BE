using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.Students.Attendance;

internal static class AttendanceActionAuthorization
{
	public static async Task EnsureAsync(IPermissionChecker checker, string permission)
	{
		if (!await checker.IsGrantedAsync(permission)) throw new AbpAuthorizationException();
	}
}
