using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Infrastructure.Auth;

/// <summary>
/// Replaces ABP's default permission checker in API tests so that
/// all permission-based [Authorize(permission)] attributes pass.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionChecker))]
public class AlwaysGrantPermissionChecker : IPermissionChecker, ITransientDependency
{
	public Task<bool> IsGrantedAsync(string name) => Task.FromResult(true);

	public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name) =>
		Task.FromResult(true);

	public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
	{
		var result = new MultiplePermissionGrantResult();
		foreach (var name in names)
		{
			result.Result[name] = PermissionGrantResult.Granted;
		}
		return Task.FromResult(result);
	}

	public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names)
	{
		return IsGrantedAsync(names);
	}
}
